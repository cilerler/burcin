using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BurcinCo.BurcinApp.Gateway.Webhook.Configuration;

internal sealed class WebhookSettings : IValidatableObject
{
	public const string ConfigurationSectionName = "Gateway:Webhook";

	/// <summary>
	/// RabbitMQ virtual host (URL-encoded, e.g. <c>%2F</c> for the default <c>/</c>).
	/// </summary>
	[Required]
	public string VHost { get; init; } = null!;

	[Range(1, 32L * 1024 * 1024)]
	public long MaxBodyBytes { get; init; } = 1_048_576;

	public TimeSpan PublishTimeout { get; init; } = TimeSpan.FromSeconds(10);

	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		if (string.IsNullOrWhiteSpace(VHost))
		{
			yield return new ValidationResult(
				"VHost is required.",
				[nameof(VHost)]);
		}
		if (PublishTimeout <= TimeSpan.Zero)
		{
			yield return new ValidationResult(
				"PublishTimeout must be greater than zero.",
				[nameof(PublishTimeout)]);
		}
	}
}
