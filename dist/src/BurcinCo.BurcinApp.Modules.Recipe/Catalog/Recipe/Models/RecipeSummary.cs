namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.Recipe.Models;

/// <summary>
/// Read-only projection returned by the <c>GetSummary()</c> OData function bound to Recipe.
/// Joins values from Recipe, Chef, and CategoryCode in a single response so the client doesn't
/// need to issue separate <c>$expand</c> calls just to render a "recipe card" view.
///
/// Declared in the EDM as a complex type (the <c>ODataConventionModelBuilder</c> picks it up
/// automatically because it's the return type of a registered function). Not an entity — has no
/// key, no entity set, no CRUD endpoints.
/// </summary>
public sealed class RecipeSummary
{
	public long RecipeId { get; set; }

	public string RecipeName { get; set; } = string.Empty;

	public string ChefName { get; set; } = string.Empty;

	public string? CategoryName { get; set; }

	/// <summary>Computed total grams = GramPerYield × Yield.</summary>
	public float GramTotal { get; set; }
}
