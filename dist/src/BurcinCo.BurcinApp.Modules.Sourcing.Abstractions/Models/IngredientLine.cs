namespace BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Models;

/// <summary>
/// One line item in an ingredient quote request — name + quantity + unit.
/// Public DTO; safe to expose at HTTP, broker, and cross-module boundaries.
/// </summary>
public record IngredientLine(
	string Name,
	float Quantity,
	string Unit);
