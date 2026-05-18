using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using BurcinCo.BurcinApp.Modules.Recipe.Catalog.RecipePhoto.Configuration;
using BurcinCo.BurcinApp.Modules.Recipe.Catalog.RecipePhoto.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.RecipePhoto;

/// <summary>
/// Stateless HMAC-SHA256-signed-URL implementation. Token wire format:
///   <c>{recipeIdBase10}.{expiryUnixSeconds}.{hmacBase64Url}</c>
/// The HMAC covers the prefix <c>{recipeId}.{expiryUnixSeconds}</c>; verification recomputes and
/// uses constant-time compare. Same shape as a stripped-down JWT but with much less ceremony —
/// fine when the only audience is your own download endpoint.
///
/// Singleton lifetime because there's no per-request state and HMAC instantiation is cheap; we
/// rebuild the HMAC per call so secret rotation (via <c>IOptionsMonitor</c>) takes effect on the
/// next request without restart.
/// </summary>
internal sealed partial class RecipePhotoService : IRecipePhotoService
{
	private static readonly ActivitySource _activitySource = new(Constants.Activities.ActivitySourceName);

	private readonly IOptionsMonitor<RecipePhotoSettings> _options;
	private readonly TimeProvider _clock;
	private readonly ILogger<RecipePhotoService> _logger;

	private readonly Counter<long> _signedUrlIssued;
	private readonly Counter<long> _downloadRejected;

	public RecipePhotoService(
		IOptionsMonitor<RecipePhotoSettings> options,
		TimeProvider clock,
		IMeterFactory meterFactory,
		ILogger<RecipePhotoService> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(clock);
		ArgumentNullException.ThrowIfNull(meterFactory);
		ArgumentNullException.ThrowIfNull(logger);
		_options = options;
		_clock = clock;
		_logger = logger;

		var meter = meterFactory.Create(Constants.Metrics.MeterName);
		_signedUrlIssued = meter.CreateCounter<long>(Constants.Metrics.SignedUrlIssued, unit: "{url}");
		_downloadRejected = meter.CreateCounter<long>(Constants.Metrics.DownloadRejected, unit: "{rejection}");
	}

	public (string Token, DateTimeOffset ExpiresAt) IssueToken(long recipeId)
	{
		using var activity = _activitySource.StartActivity(nameof(IssueToken));
		activity?.SetTag(Constants.Tags.RecipeId, recipeId);

		var settings = _options.CurrentValue;
		var expiresAt = _clock.GetUtcNow().AddSeconds(settings.ExpirySeconds);
		var expiryUnixSeconds = expiresAt.ToUnixTimeSeconds();
		var prefix = $"{recipeId}.{expiryUnixSeconds}";
		var signature = ComputeSignature(prefix, settings.SecretKey);
		var token = $"{prefix}.{signature}";

		_signedUrlIssued.Add(1, new KeyValuePair<string, object?>(Constants.Tags.RecipeId, recipeId));
		LogSignedUrlIssued(recipeId, expiresAt);
		return (token, expiresAt);
	}

	public long? ValidateToken(string token)
	{
		using var activity = _activitySource.StartActivity(nameof(ValidateToken));
		if (string.IsNullOrEmpty(token)) return RejectedAs("empty");

		var parts = token.Split('.');
		if (parts.Length != 3) return RejectedAs("malformed");
		if (!long.TryParse(parts[0], out var recipeId)) return RejectedAs("bad-recipe-id");
		if (!long.TryParse(parts[1], out var expiryUnixSeconds)) return RejectedAs("bad-expiry");

		var settings = _options.CurrentValue;
		var prefix = $"{recipeId}.{expiryUnixSeconds}";
		var expected = ComputeSignature(prefix, settings.SecretKey);
		// Constant-time compare to defeat timing-based brute-forcing.
		if (!CryptographicOperations.FixedTimeEquals(
				Encoding.UTF8.GetBytes(parts[2]),
				Encoding.UTF8.GetBytes(expected)))
		{
			return RejectedAs("bad-signature");
		}

		var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expiryUnixSeconds);
		if (expiresAt <= _clock.GetUtcNow()) return RejectedAs("expired");

		activity?.SetTag(Constants.Tags.RecipeId, recipeId);
		return recipeId;
	}

	private long? RejectedAs(string reason)
	{
		_downloadRejected.Add(1, new KeyValuePair<string, object?>(Constants.Tags.TokenRejectedReason, reason));
		LogTokenRejected(reason);
		return null;
	}

	private static string ComputeSignature(string prefix, string secret)
	{
		using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
		var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(prefix));
		// Base64url (no padding) so the token is URL-safe without escaping.
		return Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
	}

	[LoggerMessage(EventId = 3301, Level = LogLevel.Information, Message = "RecipePhoto signed URL issued. RecipeId={RecipeId} ExpiresAt={ExpiresAt:O}")]
	private partial void LogSignedUrlIssued(long recipeId, DateTimeOffset expiresAt);

	[LoggerMessage(EventId = 3302, Level = LogLevel.Warning, Message = "RecipePhoto token rejected: {Reason}.")]
	private partial void LogTokenRejected(string reason);
}
