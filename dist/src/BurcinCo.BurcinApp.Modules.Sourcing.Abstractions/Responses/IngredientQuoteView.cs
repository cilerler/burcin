using System;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Responses;

/// <summary>
/// Public read projection of an <c>IngredientQuote</c> row — captures the lifecycle status
/// without exposing the entity directly.
/// </summary>
public record IngredientQuoteView(
	long Id,
	long? RecipeId,
	string SupplierKey,
	string Status,
	DateTime RequestedAt,
	DateTime? SentAt,
	DateTime? ResponseReceivedAt,
	string? ResponseJson,
	string? FailureReason);
