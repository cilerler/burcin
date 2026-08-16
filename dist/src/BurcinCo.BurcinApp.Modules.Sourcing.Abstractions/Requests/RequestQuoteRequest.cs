using System.Collections.Generic;
using System.Text.Json.Serialization;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Models;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Requests;

/// <summary>
/// Public request DTO: ask Modules.Sourcing to fetch an ingredient quote from a configured supplier.
/// </summary>
public record RequestQuoteRequest(
	[property: JsonPropertyName("supplierKey")] string SupplierKey,
	[property: JsonPropertyName("recipeId")] long? RecipeId,
	[property: JsonPropertyName("ingredients")] IReadOnlyList<IngredientLine> Ingredients);
