namespace BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Events;

/// <summary>
/// Inbound event shape — the payload pulled from a Gateway-published webhook on the
/// <c>webhooks.sourcing.quote-response</c> routing key. The Gateway forwards the raw
/// JSON body of the supplier's HTTP POST; this record matches the agreed contract with
/// the external supplier.
/// </summary>
public record IngredientQuoteResponseReceivedEvent(
	long QuoteId,
	string SupplierKey,
	bool Accepted,
	string? RawResponseJson,
	string? Reason);
