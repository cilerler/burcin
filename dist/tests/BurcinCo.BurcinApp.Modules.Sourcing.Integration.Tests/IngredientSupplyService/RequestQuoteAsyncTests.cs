using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BurcinCo.BurcinApp.Data;
using BurcinCo.BurcinApp.Models.BurcinDatabase;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Interfaces;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Models;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Requests;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Integration.Tests.IngredientSupplyService;

/// <summary>
/// Producer side of the Sourcing flow: <c>RequestQuoteAsync</c> must persist the IngredientQuote row
/// and the Outbox event in the same transaction, so a partial failure cannot leave one without the other.
/// Guards the SaveChanges interceptor wiring: without it, Outbox rows never reach the table.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class RequestQuoteAsyncTests
{
	[TestInitialize]
	public Task TestInitializeAsync() => Initialize.Fixture.CleanTablesAsync();

	[TestMethod]
	public async Task RequestQuoteAsync_ValidRequest_PersistsQuoteAndOutboxRowAtomically()
	{
		// Arrange
		await using var scope = Initialize.Fixture.CreateScope();
		var sut = scope.ServiceProvider.GetRequiredService<ISourcingService>();
		var request = new RequestQuoteRequest(
			SupplierKey: "test-supplier",
			RecipeId: null,
			Ingredients: new[] { new IngredientLine("flour", 100f, "g") });

		// Act
		var view = await sut.RequestQuoteAsync(request);

		// Assert — return shape
		Assert.IsTrue(view.Id > 0, "Expected a generated Id from EF.");
		Assert.AreEqual(IngredientQuoteStatus.Pending, view.Status);
		Assert.AreEqual("test-supplier", view.SupplierKey);

		// Assert — persistence: one IngredientQuote row, one Outbox row, both written in the same transaction
		var db = scope.ServiceProvider.GetRequiredService<BurcinDatabaseDbContext>();
		var quote = await db.IngredientQuotes.AsNoTracking().SingleAsync();
		Assert.AreEqual(view.Id, quote.Id);
		Assert.AreEqual(IngredientQuoteStatus.Pending, quote.Status);

		var outboxCount = await db.Database
			.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM dbo.Outbox")
			.SingleAsync();
		Assert.AreEqual(1, outboxCount, "Expected exactly one Outbox row flushed by the SaveChanges interceptor.");
	}
}
