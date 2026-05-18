using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BurcinCo.BurcinApp.Data;
using BurcinCo.BurcinApp.Modules.Nutrition.Integration.Tests.Fixtures;
using BurcinCo.BurcinApp.Modules.Nutrition.Tracking.NutritionFact.Contracts;
using BurcinCo.BurcinApp.Modules.Recipe.Abstractions.Interfaces;
using BurcinCo.BurcinApp.Modules.Recipe.Abstractions.Responses;
using ChefEntity = BurcinCo.BurcinApp.Models.BurcinDatabase.Chef;
using NutritionFactEntity = BurcinCo.BurcinApp.Models.BurcinDatabase.NutritionFact;
using RecipeEntity = BurcinCo.BurcinApp.Models.BurcinDatabase.Recipe;

namespace BurcinCo.BurcinApp.Modules.Nutrition.Integration.Tests.Tracking;

/// <summary>
/// NutritionFact service tests. The MEANINGFUL coverage here is the cross-module call branch:
///   <list type="bullet">
///     <item>Recipe-not-found → NutritionFact.CreateAsync returns null without persisting (the
///       handoff explicitly flagged this as the cross-module-failure-handling regression net).</item>
///     <item>Recipe-found in-process → NutritionFact persists.</item>
///     <item>Recipe-found over HTTP via <c>RecipeClient</c> → NutritionFact persists. Proves the
///       cross-module HTTP integration the handoff said was UNTESTED end-to-end.</item>
///   </list>
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class NutritionFactServiceTests
{
	[TestInitialize]
	public Task TestInitializeAsync() => Initialize.Fixture.CleanTablesAsync();

	[TestMethod]
	public async Task CreateAsync_NonExistentRecipe_ReturnsNull_AndDoesNotPersist()
	{
		await using var scope = Initialize.Fixture.CreateScopeWithLocalRecipe();
		var sut = scope.ServiceProvider.GetRequiredService<INutritionFactService>();

		var result = await sut.CreateAsync(new NutritionFactEntity
		{
			RecipeId = 99999L, // no such recipe
			CaloriesPerYield = 100,
		});

		Assert.IsNull(result);

		var db = scope.ServiceProvider.GetRequiredService<BurcinDatabaseDbContext>();
		var count = await db.NutritionFacts.CountAsync();
		Assert.AreEqual(0, count, "Expected no NutritionFact row when recipe doesn't exist.");
	}

	[TestMethod]
	public async Task CreateAsync_ExistentRecipe_LocalIRecipeService_PersistsAndReturns()
	{
		// Seed a Chef + Recipe so the cross-module call resolves.
		await using var scope = Initialize.Fixture.CreateScopeWithLocalRecipe();
		var db = scope.ServiceProvider.GetRequiredService<BurcinDatabaseDbContext>();

		var chef = new ChefEntity { Name = "Test Chef", Url = "https://example.com" };
		db.Chefs.Add(chef);
		await db.SaveChangesAsync();

		var recipe = new RecipeEntity { ChefId = chef.Id, Name = "Test Recipe", Url = "https://example.com/recipe", Yield = 4, GramPerYield = 250f };
		db.Recipes.Add(recipe);
		await db.SaveChangesAsync();

		var sut = scope.ServiceProvider.GetRequiredService<INutritionFactService>();

		// Act
		var result = await sut.CreateAsync(new NutritionFactEntity
		{
			RecipeId = recipe.Id,
			CaloriesPerYield = 350,
			ProteinGrams = 12,
		});

		// Assert
		Assert.IsNotNull(result);
		Assert.IsTrue(result.Id > 0);
		Assert.AreEqual(recipe.Id, result.RecipeId);

		var persisted = await db.NutritionFacts.AsNoTracking().SingleAsync();
		Assert.AreEqual(recipe.Id, persisted.RecipeId);
		Assert.AreEqual(350, persisted.CaloriesPerYield);
	}

	[TestMethod]
	public async Task CreateAsync_RemoteRecipe_OverHttpRecipeClient_RoundTripsAndPersists()
	{
		// Stub the Recipe-deployment HTTP backend: GET /odata/Recipe({id}) returns the entity body.
		// RecipeClient uses OData URL form (parens around the key), and the response shape is the
		// flat entity-property JSON that OData emits — extra @odata.* annotations are ignored.
		const long remoteRecipeId = 7;
		using var stub = new StubRecipeBackend(req =>
		{
			if (req.Method == HttpMethod.Get && req.RequestUri!.AbsolutePath.EndsWith($"/odata/Recipe({remoteRecipeId})", System.StringComparison.Ordinal))
			{
				var wire = new
				{
					Id = remoteRecipeId,
					ChefId = 1,
					Name = "Remote Recipe",
					Url = (string?)null,
					Yield = 4,
					GramPerYield = 250f,
					CategoryCode = (short?)null,
				};
				return new HttpResponseMessage(HttpStatusCode.OK)
				{
					Content = JsonContent.Create(wire),
				};
			}
			return new HttpResponseMessage(HttpStatusCode.NotFound);
		});

		await using var root = Initialize.Fixture.BuildRemoteRecipeRoot(stub);
		await using var scope = root.CreateAsyncScope();

		// Sanity-check: with Recipe flag OFF, IRecipeService should be the HTTP RecipeClient, not the in-process service.
		var recipeService = scope.ServiceProvider.GetRequiredService<IRecipeService>();
		Assert.AreEqual(
			"BurcinCo.BurcinApp.Modules.Nutrition.Tracking.NutritionFact.Clients.RecipeClient",
			recipeService.GetType().FullName);

		var sut = scope.ServiceProvider.GetRequiredService<INutritionFactService>();

		// Act — CreateAsync invokes IRecipeService.GetByIdAsync, which goes through the stub.
		var result = await sut.CreateAsync(new NutritionFactEntity
		{
			RecipeId = remoteRecipeId,
			CaloriesPerYield = 200,
		});

		// Assert — fact persisted; stub was hit exactly once.
		Assert.IsNotNull(result);
		Assert.AreEqual(remoteRecipeId, result.RecipeId);
		Assert.AreEqual(1, stub.ReceivedRequests.Count, "Expected exactly one HTTP call to the Recipe deployment.");
		var hit = stub.ReceivedRequests.Single();
		Assert.AreEqual(HttpMethod.Get, hit.Method);
		StringAssert.EndsWith(hit.RequestUri!.AbsolutePath, $"/odata/Recipe({remoteRecipeId})");
	}
}
