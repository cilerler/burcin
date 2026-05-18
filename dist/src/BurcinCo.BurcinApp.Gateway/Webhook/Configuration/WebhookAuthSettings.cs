using System.ComponentModel.DataAnnotations;

namespace BurcinCo.BurcinApp.Gateway.Webhook.Configuration;

public sealed class WebhookAuthSettings
{
	public const string ConfigurationSectionName = "Gateway:WebhookAuth";

	/// <summary>
	/// When false, the <c>/webhooks/{**path}</c> endpoint returns 404 regardless of auth state.
	/// </summary>
	public bool Enabled { get; init; } = true;

	/// <summary>
	/// When false, the shared-secret check is bypassed. Intended for local development only.
	/// Always keep <c>true</c> in non-development environments.
	/// </summary>
	public bool Required { get; init; } = true;

	[Required]
	public string HeaderName { get; init; } = null!;

	/// <summary>
	/// Shared secret callers must present in the configured header.
	/// Supply via user-secrets, environment variables, or a Kubernetes secret.
	/// A null/empty value combined with <c>Required=true</c> causes the filter to fail closed (401).
	/// </summary>
	public string? Secret { get; init; }
}
