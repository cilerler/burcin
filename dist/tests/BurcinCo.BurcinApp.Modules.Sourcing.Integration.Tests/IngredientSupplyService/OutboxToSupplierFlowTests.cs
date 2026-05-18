using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BurcinCo.BurcinApp.Data;
using BurcinCo.BurcinApp.Models.BurcinDatabase;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Interfaces;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Models;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Requests;
using BurcinCo.BurcinApp.Modules.Sourcing.Integration.Tests.Fixtures;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Integration.Tests.IngredientSupplyService;

/// <summary>
/// End-to-end producer flow: <c>RequestQuoteAsync</c> writes an Outbox row, the OutboxProcessor
/// drains it to RabbitMQ, the <c>QuoteRequestDispatcher</c> worker consumes it and calls the supplier,
/// the worker updates the <c>IngredientQuote</c> status. This is the regression net for the four Burcin
/// bugs that broke this exact path during the Modular Polylith arc:
///   1. Missing <c>OutboxSavingChangesInterceptor</c> wiring (Outbox rows never reached the table).
///   2. Gateway publishing to a fixed exchange while Ruya creates exchange-per-topic.
///   3. Gateway publishing raw bodies while Ruya expects MessageEnvelope.
///   4. Gateway producing PascalCase while Ruya was strict camelCase on read.
/// If any of those regress here, this test goes red.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class OutboxToSupplierFlowTests
{
	[TestInitialize]
	public Task TestInitializeAsync() => Initialize.Fixture.CleanTablesAsync();

	[TestMethod]
	public async Task OutboxProcessor_ProducesQuoteToBroker_WorkerCallsSupplier_StatusFlipsToSent()
	{
		// Arrange — supplier stub returns 200 and records each request body for assertion.
		using var stub = new StubSupplierHandler(HttpStatusCode.OK);
		using var host = await Initialize.Fixture.BuildHostAsync(stub);

		using (var scope = host.Services.CreateScope())
		{
			var sut = scope.ServiceProvider.GetRequiredService<ISourcingService>();
			var request = new RequestQuoteRequest(
				SupplierKey: "test-supplier",
				RecipeId: 42,
				Ingredients: new[] { new IngredientLine("flour", 100f, "g") });

			// Act — kick off the producer chain.
			var view = await sut.RequestQuoteAsync(request);
			Assert.AreEqual(IngredientQuoteStatus.Pending, view.Status, "Status returned to caller should be Pending; the worker flips it to Sent later.");
		}

		// Assert — wait for the worker round-trip: status must transition Pending → Sent and SentAt populated.
		await Initialize.Fixture.WaitUntilAsync(host, async sp =>
		{
			var db = sp.GetRequiredService<BurcinDatabaseDbContext>();
			var quote = await db.IngredientQuotes.AsNoTracking().SingleOrDefaultAsync();
			return quote is not null && quote.Status == IngredientQuoteStatus.Sent && quote.SentAt is not null;
		}, timeout: TimeSpan.FromSeconds(30));

		// Supplier was actually hit, exactly once, against the configured URL.
		Assert.AreEqual(1, stub.ReceivedRequests.Count, "Supplier endpoint should have been invoked once by the dispatcher worker.");
		var supplierRequest = stub.ReceivedRequests.Single();
		Assert.AreEqual("http://supplier.test/quote", supplierRequest.RequestUri!.ToString());

		// Outbox row was marked Dispatched (still present, not deleted — Ruya's behaviour).
		// Status column is tinyint backed by Ruya.Services.ReliableMessaging.Outbox.OutboxStatus
		// (Pending=0, Dispatched=1, Poisoned=2).
		await using var assertScope = host.Services.CreateAsyncScope();
		var assertDb = assertScope.ServiceProvider.GetRequiredService<BurcinDatabaseDbContext>();
		var dispatchedCount = await assertDb.Database
			.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM dbo.Outbox WHERE [Status] = 1")
			.SingleAsync();
		Assert.AreEqual(1, dispatchedCount, "Expected exactly one Outbox row in Dispatched status after the round-trip.");

		await host.StopAsync();
	}
}
