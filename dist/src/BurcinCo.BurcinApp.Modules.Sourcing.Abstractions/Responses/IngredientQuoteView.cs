using System;
using System.Text.Json.Serialization;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Responses;

/// <summary>
/// Public read projection of an <c>IngredientQuote</c> row — captures the lifecycle status
/// without exposing the entity directly.
/// </summary>
public record IngredientQuoteView(
	[property: JsonPropertyName("id")] long Id,
	[property: JsonPropertyName("recipeId")] long? RecipeId,
	[property: JsonPropertyName("supplierKey")] string SupplierKey,
	[property: JsonPropertyName("status")] string Status,
	[property: JsonPropertyName("requestedAt")] DateTimeOffset RequestedAt,
	[property: JsonPropertyName("sentAt")] DateTimeOffset? SentAt,
	[property: JsonPropertyName("responseReceivedAt")] DateTimeOffset? ResponseReceivedAt,
	[property: JsonPropertyName("responseJson")] string? ResponseJson,
	[property: JsonPropertyName("failureReason")] string? FailureReason);
