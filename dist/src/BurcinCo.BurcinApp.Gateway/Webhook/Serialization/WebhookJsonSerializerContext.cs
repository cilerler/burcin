using System.Text.Json.Serialization;

namespace BurcinCo.BurcinApp.Gateway.Webhook.Serialization;

[JsonSourceGenerationOptions(
	GenerationMode = JsonSourceGenerationMode.Default,
	WriteIndented = false)]
[JsonSerializable(typeof(WebhookMessageEnvelope))]
[JsonSerializable(typeof(RabbitMqPublishProperties))]
[JsonSerializable(typeof(RabbitMqPublishRequest))]
[JsonSerializable(typeof(RabbitMqPublishResponse))]
internal sealed partial class WebhookJsonSerializerContext : JsonSerializerContext
{
}
