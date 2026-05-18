using System;
using System.ComponentModel.DataAnnotations;

namespace BurcinCo.BurcinApp.Gateway.Webhook.Configuration;

public sealed class WebhookSettings
{
	public const string ConfigurationSectionName = "Gateway:Webhook";

	/// <summary>
	/// RabbitMQ virtual host (URL-encoded, e.g. <c>%2F</c> for the default <c>/</c>).
	/// </summary>
	[Required]
	public string VHost { get; init; } = null!;

	// No Exchange property: the exchange name is the routing key (one topic exchange per topic),
	// per Ruya's MessageQueue.RabbitMq convention. See WebhookService.PublishAsync for the derivation
	// (`var exchange = routingKey = $"webhooks.{path.Replace('/', '.')}";`). A statically configured
	// exchange would conflict with the subscriber's auto-created exchange="<topic>" pattern.

	[Range(0, 32L * 1024 * 1024)] // up to 32 MB
	public long MaxBodyBytes { get; init; } = 1_048_576; // 1 MB default

	public TimeSpan PublishTimeout { get; init; } = TimeSpan.FromSeconds(10);
}
