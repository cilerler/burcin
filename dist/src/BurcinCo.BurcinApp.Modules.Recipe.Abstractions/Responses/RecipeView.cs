namespace BurcinCo.BurcinApp.Modules.Recipe.Abstractions.Responses;

/// <summary>
/// Cross-module read projection of a Recipe.
/// Sibling modules consume this view rather than the internal entity type.
/// </summary>
public record RecipeView(
	long Id,
	long ChefId,
	string Name,
	string? Url,
	int Yield,
	float GramPerYield,
	short? CategoryCode);
