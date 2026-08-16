using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Models;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Abstractions.Events;

/// <summary>
/// Service-owned wire event persisted to the Outbox and consumed by the IngredientSupply request subscriber.
/// It remains at the service boundary because no other module consumes this contract.
/// </summary>
public sealed record IngredientQuoteRequestedEvent(
	[property: JsonPropertyName("quoteId")] long QuoteId,
	[property: JsonPropertyName("recipeId")] long? RecipeId,
	[property: JsonPropertyName("supplierKey")] string SupplierKey,
	[property: JsonPropertyName("ingredients")] IReadOnlyList<IngredientLine> Ingredients,
	[property: JsonPropertyName("requestedAt")] DateTimeOffset RequestedAt);
