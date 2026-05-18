using System;
using BurcinCo.BurcinApp.Modules.Recipe.Catalog.Recipe.Models;
using Microsoft.OData.ModelBuilder;
// Aliased imports because the file's namespace is BurcinCo.BurcinApp.Modules.Recipe.Extensions —
// `Recipe` (class) and `Tag` (class) collide with the surrounding namespaces of the same names.
using RecipeEntity = BurcinCo.BurcinApp.Models.BurcinDatabase.Recipe;
using TagEntity = BurcinCo.BurcinApp.Modules.Recipe.Catalog.Tag.Models.Tag;

namespace BurcinCo.BurcinApp.Modules.Recipe.Extensions;

/// <summary>
/// Module-private EDM contributions for Modules.Recipe: non-database entity sets (Tag, backed by an
/// in-memory store) and behaviours bound to DB entity types (the <c>GetSummary</c> function on Recipe).
///
/// <para>DB-backed entity sets (Chef, Recipe, CategoryCode, etc.) are registered centrally in
/// <c>BurcinCo.BurcinApp.Data.ODataExtensions.AddBurcinDatabaseEntitySets</c> — see that file's xmldoc
/// for why. The short version: cross-module reads are free in the polylith, so the EDM follows the
/// DbContext (always registered) rather than the active-controller set (per-deployment).</para>
///
/// <para>Host calls this only when Modules.Recipe is feature-flag-active. Tag's entity set
/// shouldn't appear in <c>$metadata</c> when its controller isn't mounted (no other module reads
/// from Tag — it's truly module-private). The function on Recipe also depends on the module's
/// services, so it's gated alongside the controller.</para>
/// </summary>
public static class ODataExtensions
{
	public static ODataConventionModelBuilder AddRecipeModuleEdmContributions(this ODataConventionModelBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Non-database demo entity. Same EDM treatment as the EF entities — OData doesn't care that
		// the controller's IQueryable comes from a ConcurrentDictionary instead of a DbSet.
		builder.EntitySet<TagEntity>("Tag");

		// Bound function: /odata/Recipe/{key}/GetSummary returns a RecipeSummary complex type joining
		// data from Recipe + Chef + CategoryCode. Demonstrates the OData function pattern (read-only,
		// entity-bound, returns derived data instead of an entity). RecipeSummary is auto-registered
		// as a complex type because it appears as a function return type. The Recipe entity SET is
		// registered centrally in Data; EntityType<T>() returns the existing type so the function
		// attaches cleanly without re-declaring the set.
		builder.EntityType<RecipeEntity>().Function("GetSummary").Returns<RecipeSummary>();

		return builder;
	}
}
