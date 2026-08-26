using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

using BurcinCo.BurcinApp.Gateway.RateLimiting.Configuration;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BurcinCo.BurcinApp.Gateway.RateLimiting.Extensions;

internal static class StartupExtensions
{
	public static IServiceCollection AddGatewayRateLimiting(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		var section = configuration.GetRequiredSection(
			GatewayRateLimitingSettings.ConfigurationSectionName);
		var settings = section.Get<GatewayRateLimitingSettings>()
			?? throw new OptionsValidationException(
				Options.DefaultName,
				typeof(GatewayRateLimitingSettings),
				["Gateway rate-limiting configuration is required."]);
		var validationFailures = settings.Validate().ToArray();
		if (validationFailures.Length > 0)
		{
			throw new OptionsValidationException(
				Options.DefaultName,
				typeof(GatewayRateLimitingSettings),
				validationFailures);
		}

		// Limiter instances and endpoint policy names are graph-changing configuration, so capture
		// and validate them once before the service provider is built.
		services.AddRateLimiter(options =>
		{
			options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
			options.OnRejected = WriteRejectionAsync;
			options.AddPolicy(
				Constants.Policies.Proxy,
				context => CreatePartition(context, settings.Proxy));
			options.AddPolicy(
				Constants.Policies.Webhook,
				context => CreatePartition(context, settings.Webhook));
		});

		return services;
	}

	private static RateLimitPartition<string> CreatePartition(
		HttpContext context,
		TokenBucketSettings settings)
	{
		var partitionKey = GetClientAddress(context);
		return RateLimitPartition.GetTokenBucketLimiter(
			partitionKey,
			_ => new TokenBucketRateLimiterOptions
			{
				AutoReplenishment = true,
				QueueLimit = settings.QueueLimit,
				QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
				ReplenishmentPeriod = settings.ReplenishmentPeriod,
				TokenLimit = settings.TokenLimit,
				TokensPerPeriod = settings.TokensPerPeriod,
			});
	}

	private static string GetClientAddress(HttpContext context)
	{
		var address = context.Connection.RemoteIpAddress;
		if (address is null)
		{
			return "unknown";
		}

		if (address.IsIPv4MappedToIPv6)
		{
			address = address.MapToIPv4();
		}

		return address.ToString();
	}

	private static async ValueTask WriteRejectionAsync(
		OnRejectedContext context,
		CancellationToken _)
	{
		if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
		{
			context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds)
				.ToString(CultureInfo.InvariantCulture);
		}

		await Results.Problem(
			statusCode: StatusCodes.Status429TooManyRequests,
			title: "Too many requests",
			detail: "The Gateway rate limit was exceeded. Retry after the indicated delay.")
			.ExecuteAsync(context.HttpContext);
	}
}
