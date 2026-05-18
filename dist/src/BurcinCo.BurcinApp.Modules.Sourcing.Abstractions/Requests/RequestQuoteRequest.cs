using System.Collections.Generic;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Models;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Requests;

/// <summary>
/// Public request DTO: ask Modules.Sourcing to fetch an ingredient quote from a configured supplier.
/// </summary>
public record RequestQuoteRequest(
	string SupplierKey,
	long? RecipeId,
	IReadOnlyList<IngredientLine> Ingredients);
