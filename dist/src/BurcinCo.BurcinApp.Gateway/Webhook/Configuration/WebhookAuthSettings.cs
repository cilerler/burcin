using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BurcinCo.BurcinApp.Gateway.Webhook.Configuration;

internal sealed class WebhookAuthSettings : IValidatableObject
{
	public const string ConfigurationSectionName = "Gateway:WebhookAuth";

	/// <summary>
	/// When false, the shared-secret check is bypassed. Intended for local development only.
	/// Always keep <c>true</c> in non-development environments.
	/// </summary>
	public bool Required { get; init; } = true;

	public string HeaderName { get; init; } = null!;

	/// <summary>
	/// Shared secret callers must present in the configured header.
	/// Supply via user-secrets, environment variables, or a Kubernetes secret.
	/// </summary>
	public string? Secret { get; init; }

	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		if (Required && string.IsNullOrWhiteSpace(HeaderName))
		{
			yield return new ValidationResult(
				"HeaderName is required when webhook ingestion is enabled.",
				[nameof(HeaderName)]);
		}
		if (Required && string.IsNullOrWhiteSpace(Secret))
		{
			yield return new ValidationResult(
				"Secret is required when webhook authentication is enabled and required.",
				[nameof(Secret)]);
		}
	}
}
