using System.Text.Json.Serialization;

namespace BurcinCo.BurcinApp.Gateway.Webhook.Serialization;

internal sealed record RabbitMqPublishProperties(
	[property: JsonPropertyName("content_type")] string ContentType,
	[property: JsonPropertyName("delivery_mode")] int DeliveryMode);

internal sealed record RabbitMqPublishRequest(
	[property: JsonPropertyName("properties")] RabbitMqPublishProperties Properties,
	[property: JsonPropertyName("routing_key")] string RoutingKey,
	[property: JsonPropertyName("payload")] string Payload,
	[property: JsonPropertyName("payload_encoding")] string PayloadEncoding);

internal sealed record RabbitMqPublishResponse(
	[property: JsonPropertyName("routed")] bool Routed);
