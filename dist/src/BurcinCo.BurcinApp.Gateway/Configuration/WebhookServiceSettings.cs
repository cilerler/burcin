using System;
using System.ComponentModel.DataAnnotations;

namespace BurcinCo.BurcinApp.Gateway.Configuration;

public sealed class WebhookServiceSettings
{
	public const string ConfigurationSectionName = "Gateway:Webhook";

	/// <summary>
	/// RabbitMQ virtual host (URL-encoded, e.g. <c>%2F</c> for the default <c>/</c>).
	/// </summary>
	[Required]
	public string VHost { get; init; } = null!;

	/// <summary>
	/// RabbitMQ exchange name to publish webhook payloads to.
	/// </summary>
	[Required]
	public string Exchange { get; init; } = null!;

	[Range(0, 32L * 1024 * 1024)] // up to 32 MB
	public long MaxBodyBytes { get; init; } = 1_048_576; // 1 MB default

	public TimeSpan PublishTimeout { get; init; } = TimeSpan.FromSeconds(10);
}
