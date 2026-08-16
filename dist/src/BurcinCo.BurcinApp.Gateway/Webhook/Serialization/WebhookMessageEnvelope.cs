using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BurcinCo.BurcinApp.Gateway.Webhook.Serialization;

internal sealed record WebhookMessageEnvelope(
	[property: JsonPropertyName("messageId")] string MessageId,
	[property: JsonPropertyName("messageType")] string MessageType,
	[property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp,
	[property: JsonPropertyName("source")] string Source,
	[property: JsonPropertyName("payload")] JsonElement Payload,
	[property: JsonPropertyName("persistent")] bool Persistent);
