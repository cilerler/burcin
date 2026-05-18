using System;
using BurcinCo.BurcinApp.Models.BurcinDatabase;
using Microsoft.OData.ModelBuilder;

namespace BurcinCo.BurcinApp.Data;

/// <summary>
/// Centralised OData EDM contribution for entities owned by the shared
/// <see cref="BurcinDatabaseDbContext"/>. Registered once by Host (unconditionally) regardless of
/// which modules are feature-flag-active in the running deployment.
///
/// <para><b>Why central, not per-module:</b> the polylith's defining advantage is that cross-module
/// reads are free — every module sees the whole DbContext via direct EF JOINs across schemas (DB
/// grants are read-everywhere, write-narrow). The EDM is the *read-surface declaration* and follows
/// the DbContext, not the active-controller set. Gating the EDM by per-module feature flags would
/// kill that advantage: <c>$expand</c> across module boundaries would fail in any deployment that
/// doesn't activate the target module, even though the data path is open.</para>
///
/// <para><b>What this means in practice:</b> <c>$metadata</c> advertises every DB-backed entity in
/// every deployment. Direct <c>GET /odata/{EntitySet}</c> still 404s when no controller is mounted —
/// controller activation is per-module via FeatureManagement flags, and that's correct (writes and
/// direct entity-set GETs belong to whoever owns the entity). But <c>GET /odata/Recipe(1)?$expand=
/// Ingredients</c> works wherever Recipe's controller is mounted, because the controller has full
/// DbContext access and OData runs the expansion server-side inside the controller's query.</para>
///
/// <para><b>What stays per-module:</b> non-database entities backed by something other than a DbSet
/// (e.g. Modules.Recipe's <c>Tag</c>, an in-memory <c>ConcurrentDictionary</c> demo) and behaviours
/// bound to DB entities (functions, actions). Those belong to the owning module and are wired
/// conditionally — see <c>Modules.Recipe.Extensions.ODataExtensions</c>.</para>
/// </summary>
public static class ODataExtensions
{
	public static ODataConventionModelBuilder AddBurcinDatabaseEntitySets(this ODataConventionModelBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Entity-set names use string literals so the URL form (/odata/Chef) matches the controller-name
		// convention (ChefController → entity set "Chef") regardless of any aliasing at the call site.
		// IsConcurrencyToken() is the OData-side marker for ETag emission — separate from the EF-side
		// IsRowVersion()+IsConcurrencyToken() that BurcinDatabaseDbContext.OnModelCreating sets for the
		// EF model. Both calls are needed: EF uses its marker for optimistic-concurrency SQL, OData uses
		// its marker for the @odata.etag header.

		// Recipe schema
		var chef = builder.EntitySet<Chef>("Chef");
		chef.EntityType.Property(e => e.RowVersion).IsConcurrencyToken();

		var recipe = builder.EntitySet<Recipe>("Recipe");
		recipe.EntityType.Property(e => e.RowVersion).IsConcurrencyToken();

		var recipeExpansion = builder.EntitySet<RecipeExpansion>("RecipeExpansion");
		recipeExpansion.EntityType.Property(e => e.RowVersion).IsConcurrencyToken();

		var categoryCode = builder.EntitySet<CategoryCode>("CategoryCode");
		categoryCode.EntityType.Property(e => e.RowVersion).IsConcurrencyToken();

		var categoryGroup = builder.EntitySet<CategoryGroup>("CategoryGroup");
		categoryGroup.EntityType.Property(e => e.RowVersion).IsConcurrencyToken();

		// Composite-key join entity. The convention builder can't infer composite keys without [Key]
		// attributes, so declare them fluently. Action-method parameters in CategoryCodeGroupMappingController
		// must be named keyCategoryCodeId / keyCategoryGroupId for OData routing to bind from the URI form
		// /odata/CategoryCodeGroupMapping(CategoryCodeId=1,CategoryGroupId=2). Slash-form
		// /odata/CategoryCodeGroupMapping/{codeId}/{groupId} is wired via explicit attribute routing on the
		// controller.
		var mapping = builder.EntitySet<CategoryCodeGroupMapping>("CategoryCodeGroupMapping");
		mapping.EntityType.HasKey(m => new { m.CategoryCodeId, m.CategoryGroupId });
		mapping.EntityType.Property(e => e.RowVersion).IsConcurrencyToken();

		// Nutrition schema
		var nutritionFact = builder.EntitySet<NutritionFact>("NutritionFact");
		nutritionFact.EntityType.Property(e => e.RowVersion).IsConcurrencyToken();

		// Sourcing schema. Registered unconditionally even though Modules.Sourcing exposes only minimal-API
		// endpoints (no OData controller for IngredientQuote). The entity sits in the shared DbContext, so
		// other modules' OData controllers can $expand into it. Direct GET /odata/IngredientQuote returns
		// 404 (no controller mounted); that's the honest signal — readable via $expand, no dedicated endpoint.
		var ingredientQuote = builder.EntitySet<IngredientQuote>("IngredientQuote");
		ingredientQuote.EntityType.Property(e => e.RowVersion).IsConcurrencyToken();

		return builder;
	}
}
