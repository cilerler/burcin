using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BurcinCo.BurcinApp.Data;
using BurcinCo.BurcinApp.Modules.Recipe.Abstractions.Interfaces;
using BurcinCo.BurcinApp.Modules.Recipe.Abstractions.Requests;
using ChefEntity = BurcinCo.BurcinApp.Models.BurcinDatabase.Chef;

namespace BurcinCo.BurcinApp.Modules.Recipe.Integration.Tests.Catalog;

/// <summary>
/// Recipe service tests. The meaningful coverage here is the FK-to-Chef path (proves
/// the per-module schema migration wired the cross-table FK correctly) and the entity → view
/// projection (proves cross-module callers receive the projection, not the entity).
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class RecipeServiceTests
{
	[TestInitialize]
	public Task TestInitializeAsync() => Initialize.Fixture.CleanTablesAsync();

	[TestMethod]
	public async Task CreateAsync_ValidChef_PersistsRecipe_ReturnsViewWithGeneratedId()
	{
		// Arrange — seed a Chef row first; Recipe FK references it.
		await using var scope = Initialize.Fixture.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<BurcinDatabaseDbContext>();
		var chef = new ChefEntity { Name = "Test Chef", Url = "https://example.com" };
		db.Chefs.Add(chef);
		await db.SaveChangesAsync();

		var sut = scope.ServiceProvider.GetRequiredService<IRecipeService>();
		var request = new RecipeCreateRequest(
			ChefId: chef.Id,
			Name: "Test Recipe",
			Url: "https://example.com/recipe",
			Yield: 4,
			GramPerYield: 250f,
			CategoryCode: null);

		// Act
		var view = await sut.CreateAsync(request);

		// Assert — view shape + persistence.
		Assert.IsTrue(view.Id > 0, "Expected a generated Id from EF.");
		Assert.AreEqual(chef.Id, view.ChefId);
		Assert.AreEqual("Test Recipe", view.Name);

		var persisted = await db.Recipes.AsNoTracking().SingleAsync(r => r.Id == view.Id);
		Assert.AreEqual(chef.Id, persisted.ChefId);
		Assert.AreEqual(4, persisted.Yield);
	}

	[TestMethod]
	public async Task GetByIdAsync_NonExistent_ReturnsNull()
	{
		await using var scope = Initialize.Fixture.CreateScope();
		var sut = scope.ServiceProvider.GetRequiredService<IRecipeService>();

		var view = await sut.GetByIdAsync(99999L);

		Assert.IsNull(view);
	}

	[TestMethod]
	public async Task UpdateAsync_NonExistent_ReturnsNull()
	{
		await using var scope = Initialize.Fixture.CreateScope();
		var sut = scope.ServiceProvider.GetRequiredService<IRecipeService>();

		var view = await sut.UpdateAsync(99999L, new RecipeCreateRequest(1, "x", null, 1, 1f, null));

		Assert.IsNull(view);
	}
}
