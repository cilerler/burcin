using System.Text.Json.Serialization;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Events;

/// <summary>
/// Inbound event shape — the payload pulled from a Gateway Webhook envelope on the
/// <c>webhooks.sourcing.quote-response</c> routing key. The Gateway edge adapter translates
/// the supplier's HTTP POST into the broker envelope; this record matches the agreed payload
/// contract with the external supplier.
/// </summary>
public record IngredientQuoteResponseReceivedEvent(
	[property: JsonPropertyName("quoteId")] long QuoteId,
	[property: JsonPropertyName("supplierKey")] string SupplierKey,
	[property: JsonPropertyName("accepted")] bool Accepted,
	[property: JsonPropertyName("rawResponseJson")] string? RawResponseJson,
	[property: JsonPropertyName("reason")] string? Reason);
