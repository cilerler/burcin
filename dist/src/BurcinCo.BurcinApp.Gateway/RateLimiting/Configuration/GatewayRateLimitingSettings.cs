using System;
using System.Collections.Generic;

namespace BurcinCo.BurcinApp.Gateway.RateLimiting.Configuration;

internal sealed class GatewayRateLimitingSettings
{
	public const string ConfigurationSectionName = "Gateway:RateLimiting";

	public TokenBucketSettings Proxy { get; set; } = new();

	public TokenBucketSettings Webhook { get; set; } = new();

	public IEnumerable<string> Validate()
	{
		foreach (var failure in Proxy.Validate(nameof(Proxy)))
		{
			yield return failure;
		}

		foreach (var failure in Webhook.Validate(nameof(Webhook)))
		{
			yield return failure;
		}
	}
}

internal sealed class TokenBucketSettings
{
	public int TokenLimit { get; set; }

	public int TokensPerPeriod { get; set; }

	public TimeSpan ReplenishmentPeriod { get; set; }

	public int QueueLimit { get; set; }

	public IEnumerable<string> Validate(string policyName)
	{
		if (TokenLimit <= 0)
		{
			yield return $"{policyName}:TokenLimit must be greater than zero.";
		}

		if (TokensPerPeriod <= 0)
		{
			yield return $"{policyName}:TokensPerPeriod must be greater than zero.";
		}

		if (TokensPerPeriod > TokenLimit)
		{
			yield return $"{policyName}:TokensPerPeriod cannot exceed TokenLimit.";
		}

		if (ReplenishmentPeriod <= TimeSpan.Zero)
		{
			yield return $"{policyName}:ReplenishmentPeriod must be greater than zero.";
		}

		if (QueueLimit < 0)
		{
			yield return $"{policyName}:QueueLimit cannot be negative.";
		}
	}
}
