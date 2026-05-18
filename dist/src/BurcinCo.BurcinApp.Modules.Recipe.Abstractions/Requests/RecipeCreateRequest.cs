namespace BurcinCo.BurcinApp.Modules.Recipe.Abstractions.Requests;

/// <summary>
/// Cross-module request DTO for creating or updating a Recipe.
/// Passed by value across module boundaries (in-process or HTTP).
/// </summary>
public record RecipeCreateRequest(
	long ChefId,
	string Name,
	string? Url,
	int Yield,
	float GramPerYield,
	short? CategoryCode);
