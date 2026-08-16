using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BurcinCo.BurcinApp.Data;
using BurcinCo.BurcinApp.Models.BurcinDatabase;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Interfaces;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Models;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Requests;
using BurcinCo.BurcinApp.Modules.Sourcing.Integration.Tests.Fixtures;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Abstractions.Events;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Contracts;
using SourcingIngredientSupplyService = BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.IngredientSupplyService;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Integration.Tests.IngredientSupplyService;

/// <summary>
/// End-to-end producer flow: <c>RequestQuoteAsync</c> writes an Outbox row, the OutboxProcessor
/// drains it to RabbitMQ, the <c>IngredientQuoteRequestedEventSubscriber</c> consumes it and delegates the supplier call,
/// the business service updates the <c>IngredientQuote</c> status. Four important failure modes are covered here:
///   1. Missing <c>OutboxSavingChangesInterceptor</c> wiring (Outbox rows never reach the table).
///   2. An Outbox envelope omitting the configured provider and falling into an unusable global fallback.
///   3. A broker redelivery repeating the irreversible supplier call.
///   4. Transient and permanent supplier failures mapping to the wrong retry/reject result.
/// If any of those regress here, this test goes red.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class OutboxToSupplierFlowTests
{
	private const string IngredientQuoteRequestedEventTopicName = "sourcing.ingredient-quote.requested";
	private const string RequestQueue = IngredientQuoteRequestedEventTopicName + ".queue";

	[TestInitialize]
	public Task TestInitializeAsync() => Initialize.Fixture.CleanTablesAsync();

	[TestMethod]
	public async Task OutboxProcessor_ProducesQuoteToBroker_SubscriberDelegatesSupplierCall_StatusFlipsToSent()
	{
		// Arrange — supplier stub returns 200 and records each request body for assertion.
		using var stub = new StubSupplierHandler(HttpStatusCode.OK);
		var invocationState = new RequestInvocationState();
		using var host = await Initialize.Fixture.BuildHostAsync(stub, services =>
		{
			services.RemoveAll<IIngredientSupply>();
			services.AddSingleton(invocationState);
			services.AddScoped<IIngredientSupply>(provider =>
				new RequestInvocationTrackingIngredientSupplyService(
					provider.GetRequiredService<SourcingIngredientSupplyService>(),
					provider.GetRequiredService<RequestInvocationState>()));
		});

		long quoteId;
		var ingredients = new[] { new IngredientLine("flour", 100f, "g") };
		using (var scope = host.Services.CreateScope())
		{
			var sut = scope.ServiceProvider.GetRequiredService<ISourcingService>();
			var request = new RequestQuoteRequest(
				SupplierKey: "test-supplier",
				RecipeId: 42,
				Ingredients: ingredients);

			// Act — kick off the producer chain.
			var view = await sut.RequestQuoteAsync(request, CancellationToken.None);
			quoteId = view.Id;
			Assert.AreEqual(IngredientQuoteStatus.Pending, view.Status, "Status returned to caller should be Pending; the subscriber delegates the later Sent transition.");
		}

		// Assert — wait for the subscriber round-trip: status must transition Pending → Sent and SentAt populated.
		await Initialize.Fixture.WaitUntilAsync(host, async sp =>
		{
			var db = sp.GetRequiredService<BurcinDatabaseDbContext>();
			var quote = await db.IngredientQuotes.AsNoTracking().SingleOrDefaultAsync();
			return quote is not null && quote.Status == IngredientQuoteStatus.Sent && quote.SentAt is not null;
		}, timeout: TimeSpan.FromSeconds(30));

		// Supplier was actually hit, exactly once, against the configured URL.
		Assert.AreEqual(1, stub.ReceivedRequests.Count, "Supplier endpoint should have been invoked once by the quote-request subscriber.");
		var supplierRequest = stub.ReceivedRequests.Single();
		Assert.AreEqual("http://supplier.test/quote", supplierRequest.RequestUri!.ToString());
		Assert.AreEqual(
			quoteId.ToString(System.Globalization.CultureInfo.InvariantCulture),
			supplierRequest.Headers.GetValues("Idempotency-Key").Single(),
			"A redelivered quote request must carry the same supplier-side idempotency key.");

		// A later broker delivery with a different MessageId still represents the same quote request.
		// The committed Sent state must suppress a second irreversible supplier call.
		await using (var publisher = await RawBrokerPublisher.ConnectAsync(
			Initialize.Fixture.RabbitMqHost, Initialize.Fixture.RabbitMqPort))
		{
			var redelivery = new IngredientQuoteRequestedEvent(
				quoteId,
				RecipeId: 42,
				SupplierKey: "test-supplier",
				Ingredients: ingredients,
				RequestedAt: DateTimeOffset.UtcNow);
			await publisher.PublishEnvelopeAsync(
				IngredientQuoteRequestedEventTopicName,
				Guid.NewGuid().ToString("N"),
				redelivery);
			await Initialize.Fixture.WaitUntilAsync(host, _ =>
				Task.FromResult(invocationState.InvocationCount == 2));
		}
		Assert.AreEqual(2, invocationState.InvocationCount, "Both broker deliveries must reach the business handler.");
		Assert.AreEqual(1, stub.ReceivedRequests.Count, "A redelivered request for an already-Sent quote must not call the supplier again.");

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

	[TestMethod]
	public async Task SupplierReturnsServiceUnavailable_RetriesOnceWithStableIdempotencyKey_ThenDeadLettersWithoutMutation()
	{
		using var stub = new StubSupplierHandler(HttpStatusCode.ServiceUnavailable);
		using var host = await Initialize.Fixture.BuildHostAsync(stub);
		await using var publisher = await RawBrokerPublisher.ConnectAsync(
			Initialize.Fixture.RabbitMqHost, Initialize.Fixture.RabbitMqPort);
		await WaitForRequestQueueAsync(host, publisher);
		var dlqName = $"{IngredientQuoteRequestedEventTopicName}.dlq";
		var initialDlqDepth = await publisher.GetQueueDepthAsync(dlqName);

		long quoteId;
		using (var scope = host.Services.CreateScope())
		{
			var sut = scope.ServiceProvider.GetRequiredService<ISourcingService>();
			var view = await sut.RequestQuoteAsync(new RequestQuoteRequest(
				SupplierKey: "test-supplier",
				RecipeId: null,
				Ingredients: new[] { new IngredientLine("flour", 100f, "g") }),
				CancellationToken.None);
			quoteId = view.Id;
		}

		await Initialize.Fixture.WaitUntilAsync(host, async _ =>
			await publisher.GetQueueDepthAsync(dlqName) > initialDlqDepth);

		Assert.AreEqual(2, stub.ReceivedRequests.Count, "A transient supplier response gets one delayed retry before the finite delivery cap rejects it.");
		var idempotencyKeys = stub.ReceivedRequests
			.Select(request => request.Headers.GetValues("Idempotency-Key").Single())
			.Distinct(StringComparer.Ordinal)
			.ToArray();
		Assert.AreEqual(1, idempotencyKeys.Length, "Every supplier attempt for the same quote must carry the same idempotency key.");
		Assert.AreEqual(quoteId.ToString(System.Globalization.CultureInfo.InvariantCulture), idempotencyKeys[0]);

		await using var verificationScope = host.Services.CreateAsyncScope();
		var db = verificationScope.ServiceProvider.GetRequiredService<BurcinDatabaseDbContext>();
		var quote = await db.IngredientQuotes.AsNoTracking().SingleAsync(q => q.Id == quoteId);
		Assert.AreEqual(IngredientQuoteStatus.Pending, quote.Status, "Transient supplier failures must not commit a terminal business mutation.");
		Assert.IsNull(quote.SentAt);

		await host.StopAsync();
	}

	[TestMethod]
	public async Task SupplierReturnsBadRequest_RejectsOnceWithoutRetryOrMutation()
	{
		using var stub = new StubSupplierHandler(HttpStatusCode.BadRequest);
		using var host = await Initialize.Fixture.BuildHostAsync(stub);
		await using var publisher = await RawBrokerPublisher.ConnectAsync(
			Initialize.Fixture.RabbitMqHost, Initialize.Fixture.RabbitMqPort);
		await WaitForRequestQueueAsync(host, publisher);
		var dlqName = $"{IngredientQuoteRequestedEventTopicName}.dlq";
		var initialDlqDepth = await publisher.GetQueueDepthAsync(dlqName);

		long quoteId;
		using (var scope = host.Services.CreateScope())
		{
			var sut = scope.ServiceProvider.GetRequiredService<ISourcingService>();
			var view = await sut.RequestQuoteAsync(new RequestQuoteRequest(
				SupplierKey: "test-supplier",
				RecipeId: null,
				Ingredients: new[] { new IngredientLine("flour", 100f, "g") }),
				CancellationToken.None);
			quoteId = view.Id;
		}

		await Initialize.Fixture.WaitUntilAsync(host, async _ =>
			await publisher.GetQueueDepthAsync(dlqName) > initialDlqDepth);

		Assert.AreEqual(1, stub.ReceivedRequests.Count, "A permanent supplier rejection must go directly to the DLQ without retry.");
		await using var verificationScope = host.Services.CreateAsyncScope();
		var db = verificationScope.ServiceProvider.GetRequiredService<BurcinDatabaseDbContext>();
		var quote = await db.IngredientQuotes.AsNoTracking().SingleAsync(q => q.Id == quoteId);
		Assert.AreEqual(IngredientQuoteStatus.Pending, quote.Status, "A permanent HTTP rejection must be classified before business mutation.");
		Assert.IsNull(quote.SentAt);

		await host.StopAsync();
	}

	[TestMethod]
	public async Task Subscriber_MissingLocalQuote_RejectsBeforeCallingSupplier()
	{
		using var stub = new StubSupplierHandler(HttpStatusCode.OK);
		using var host = await Initialize.Fixture.BuildHostAsync(stub);
		await using var publisher = await RawBrokerPublisher.ConnectAsync(
			Initialize.Fixture.RabbitMqHost, Initialize.Fixture.RabbitMqPort);
		await WaitForRequestQueueAsync(host, publisher);
		var dlqName = $"{IngredientQuoteRequestedEventTopicName}.dlq";
		var initialDlqDepth = await publisher.GetQueueDepthAsync(dlqName);
		var payload = new IngredientQuoteRequestedEvent(
			QuoteId: 999_999,
			RecipeId: null,
			SupplierKey: "test-supplier",
			Ingredients: new[] { new IngredientLine("flour", 100f, "g") },
			RequestedAt: DateTimeOffset.UtcNow);

		await publisher.PublishEnvelopeAsync(
			IngredientQuoteRequestedEventTopicName,
			Guid.NewGuid().ToString("N"),
			payload);
		await Initialize.Fixture.WaitUntilAsync(host, async _ =>
			await publisher.GetQueueDepthAsync(dlqName) > initialDlqDepth);

		Assert.AreEqual(0, stub.ReceivedRequests.Count, "Quote existence must be checked before the irreversible supplier call.");
		await using var scope = host.Services.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<BurcinDatabaseDbContext>();
		Assert.AreEqual(0, await db.IngredientQuotes.CountAsync());

		await host.StopAsync();
	}

	private static Task WaitForRequestQueueAsync(
		Microsoft.Extensions.Hosting.IHost host,
		RawBrokerPublisher publisher) =>
		Initialize.Fixture.WaitUntilAsync(host, async _ =>
		{
			try
			{
				await publisher.GetQueueDepthAsync(RequestQueue);
				return true;
			}
			catch
			{
				return false;
			}
		});
}
