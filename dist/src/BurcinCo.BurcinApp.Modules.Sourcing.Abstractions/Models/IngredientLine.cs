using System.Text.Json.Serialization;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Models;

/// <summary>
/// One line item in an ingredient quote request — name + quantity + unit.
/// Public DTO; safe to expose at HTTP, broker, and cross-module boundaries.
/// </summary>
public record IngredientLine(
	[property: JsonPropertyName("name")] string Name,
	[property: JsonPropertyName("quantity")] float Quantity,
	[property: JsonPropertyName("unit")] string Unit);
