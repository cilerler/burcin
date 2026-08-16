using System;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BurcinCo.BurcinApp.Data;
using BurcinCo.BurcinApp.Models.BurcinDatabase;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Events;
using BurcinCo.BurcinApp.Modules.Sourcing.Integration.Tests.Fixtures;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Contracts;
using IngredientSupplyConstants = BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Constants;
using SourcingIngredientSupplyService = BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.IngredientSupplyService;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Integration.Tests.IngredientSupplyService;

/// <summary>
/// Inbound consumer-side flows. These exercise <c>IngredientQuoteResponseReceivedEventSubscriber</c> +
/// <c>SubscribeWithInboxAndPostCommitAsync</c> + <c>IngredientSupplyService</c> end-to-end against a real broker:
///   <list type="bullet">
///     <item>Inbox dedup — duplicate <c>MessageId</c> doesn't double-process. Regression net for the
///       reason the Inbox exists at all.</item>
///     <item>Retry rollback — the first mutation and claim roll back, the redelivery commits, and the
///       post-commit business metric reports exactly once.</item>
///     <item>Invalid response invariants — permanent routing/payload failures reject before mutation.</item>
///     <item>Poison message → DLQ — invalid JSON gets rejected ONCE, lands in the DLQ via auto-wired DLX,
///       no infinite redelivery loop. Regression net for Ruya bug #6 (the 4.6M-redelivery loop).</item>
///     <item>Case-insensitive deserialize — PascalCase envelope payload deserializes successfully.
///       Regression net for Ruya bug #7.</item>
///   </list>
/// Response tests seed a Sent <c>IngredientQuote</c>; a response may only complete a request whose
/// outbound transition was committed.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class InboundConsumerFlowTests
{
	private const string IngredientQuoteResponseReceivedEventTopicName = "webhooks.sourcing.quote-response";
	private const string IngredientQuoteRequestedEventTopicName = "sourcing.ingredient-quote.requested";
	// Ruya RabbitMQ topology: queue name = `{topic}.queue` when SubscribeOptions.ConsumerGroup is not set
	// (the explicit `consumerName` parameter is for Inbox-dedup keying, not for queue naming).
	private const string ResponseQueue = IngredientQuoteResponseReceivedEventTopicName + ".queue";
	private const string RequestQueue = IngredientQuoteRequestedEventTopicName + ".queue";

	[TestInitialize]
	public Task TestInitializeAsync() => Initialize.Fixture.CleanTablesAsync();

	[TestMethod]
	public async Task SubscribeWithInboxAndPostCommit_DuplicateMessageId_ProcessesOnce()
	{
		// Arrange — supplier stub never invoked on this path (inbound, not outbound), but BuildHostAsync
		// requires a handler. Seed a quote row first so the inbound handler has something to update.
		using var stub = new StubSupplierHandler(HttpStatusCode.OK);
		var invocationState = new ResponseInvocationState();
		using var host = await Initialize.Fixture.BuildHostAsync(stub, services =>
		{
			services.RemoveAll<IIngredientSupply>();
			services.AddSingleton(invocationState);
			services.AddScoped<IIngredientSupply>(provider =>
				new ResponseInvocationTrackingIngredientSupplyService(
					provider.GetRequiredService<SourcingIngredientSupplyService>(),
					provider.GetRequiredService<ResponseInvocationState>()));
		});
		var quoteId = await SeedSentQuoteAsync(host);

		await using var publisher = await RawBrokerPublisher.ConnectAsync(
			Initialize.Fixture.RabbitMqHost, Initialize.Fixture.RabbitMqPort);

		var messageId = Guid.NewGuid().ToString("N");
		var payload = new IngredientQuoteResponseReceivedEvent(
			QuoteId: quoteId,
			SupplierKey: "test-supplier",
			Accepted: true,
			RawResponseJson: """{"ok":true}""",
			Reason: null);

		// Act — publish the same envelope twice.
		await publisher.PublishEnvelopeAsync(IngredientQuoteResponseReceivedEventTopicName, messageId, payload);
		await publisher.PublishEnvelopeAsync(IngredientQuoteResponseReceivedEventTopicName, messageId, payload);

		// First delivery flips status to ResponseReceived.
		await Initialize.Fixture.WaitUntilAsync(host, async sp =>
		{
			var db = sp.GetRequiredService<BurcinDatabaseDbContext>();
			var q = await db.IngredientQuotes.AsNoTracking().SingleAsync();
			return q.Status == IngredientQuoteStatus.ResponseReceived;
		});

		// Wait one beat for the broker to deliver the duplicate (if it's going to).
		await Task.Delay(TimeSpan.FromSeconds(2));

		// Assert — exactly ONE Inbox row (Inbox.Status==Processed, MessageId == published id).
		await using var scope = host.Services.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<BurcinDatabaseDbContext>();
		var inboxCount = await db.Database
			.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM dbo.Inbox WHERE [MessageId] = {0}", messageId)
			.SingleAsync();
		Assert.AreEqual(1, inboxCount, "Expected exactly one Inbox row — duplicate delivery should be suppressed by the Inbox dedup check.");
		Assert.AreEqual(1, invocationState.InvocationCount, "The duplicate MessageId must be acknowledged without invoking the business handler again.");

		// Quote was updated exactly once — ResponseReceivedAt is set, ResponseJson populated, no double mutation.
		var quote = await db.IngredientQuotes.AsNoTracking().SingleAsync();
		Assert.AreEqual(IngredientQuoteStatus.ResponseReceived, quote.Status);
		Assert.IsNotNull(quote.ResponseReceivedAt);

		await host.StopAsync();
	}

	[TestMethod]
	public async Task SubscribeWithInboxAndPostCommit_ConcurrencyRetry_RollsBackClaimAndMutation_ThenProcessesRedelivery()
	{
		long committedResponseMeasurements = 0;
		using var meterListener = new MeterListener
		{
			InstrumentPublished = (instrument, listener) =>
			{
				if (instrument.Meter.Name == IngredientSupplyConstants.Metrics.MeterName &&
					instrument.Name == IngredientSupplyConstants.Metrics.QuoteResponseReceived)
				{
					listener.EnableMeasurementEvents(instrument);
				}
			},
		};
		meterListener.SetMeasurementEventCallback<long>((_, measurement, _, _) =>
			Interlocked.Add(ref committedResponseMeasurements, measurement));
		meterListener.Start();

		using var stub = new StubSupplierHandler(HttpStatusCode.OK);
		var retryState = new ConcurrencyRetryState();
		using var host = await Initialize.Fixture.BuildHostAsync(stub, services =>
		{
			services.RemoveAll<IIngredientSupply>();
			services.AddSingleton(retryState);
			services.AddScoped<IIngredientSupply>(provider =>
				new ConcurrencyRetryIngredientSupplyService(
					provider.GetRequiredService<SourcingIngredientSupplyService>(),
					provider.GetRequiredService<ConcurrencyRetryState>()));
		});
		var quoteId = await SeedSentQuoteAsync(host);

		await using var publisher = await RawBrokerPublisher.ConnectAsync(
			Initialize.Fixture.RabbitMqHost, Initialize.Fixture.RabbitMqPort);

		var messageId = Guid.NewGuid().ToString("N");
		var payload = new IngredientQuoteResponseReceivedEvent(
			QuoteId: quoteId,
			SupplierKey: "test-supplier",
			Accepted: true,
			RawResponseJson: """{"ok":true}""",
			Reason: null);

		await publisher.PublishEnvelopeAsync(IngredientQuoteResponseReceivedEventTopicName, messageId, payload);

		await Initialize.Fixture.WaitUntilAsync(host, async serviceProvider =>
		{
			if (retryState.Attempts < 2)
			{
				return false;
			}

			var db = serviceProvider.GetRequiredService<BurcinDatabaseDbContext>();
			var quote = await db.IngredientQuotes.AsNoTracking().SingleAsync();
			return quote.Status == IngredientQuoteStatus.ResponseReceived;
		});
		await Initialize.Fixture.WaitUntilAsync(host, _ =>
			Task.FromResult(Volatile.Read(ref committedResponseMeasurements) == 1));

		await using var scope = host.Services.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<BurcinDatabaseDbContext>();
		var processedInboxCount = await db.Database
			.SqlQueryRaw<int>(
				"SELECT COUNT(*) AS Value FROM dbo.Inbox WHERE [MessageId] = {0} AND [Status] = 1",
				messageId)
			.SingleAsync();

		Assert.AreEqual(2, retryState.Attempts, "The same delivery should run once before and once after the explicit retry.");
		Assert.AreEqual(1, processedInboxCount, "Only the successful redelivery may commit the Inbox claim.");
		var quote = await db.IngredientQuotes.AsNoTracking().SingleAsync();
		Assert.AreEqual(
			"{\"attempt\":2}",
			quote.ResponseJson,
			"The first attempt's distinct mutation must roll back; only the redelivery marker may commit.");
		Assert.AreEqual(
			1L,
			Volatile.Read(ref committedResponseMeasurements),
			"Committed-work telemetry must run once after the successful commit, not once per atomic callback attempt.");

		await host.StopAsync();
	}

	[DataTestMethod]
	[DataRow("different-supplier", "Supplier cannot quote the request.")]
	[DataRow("test-supplier", " ")]
	public async Task Subscriber_InvalidSupplierResponse_RejectsOnceWithoutMutationOrCommittedInboxClaim(
		string supplierKey,
		string reason)
	{
		using var stub = new StubSupplierHandler(HttpStatusCode.OK);
		var invocationState = new ResponseInvocationState();
		using var host = await Initialize.Fixture.BuildHostAsync(stub, services =>
		{
			services.RemoveAll<IIngredientSupply>();
			services.AddSingleton(invocationState);
			services.AddScoped<IIngredientSupply>(provider =>
				new ResponseInvocationTrackingIngredientSupplyService(
					provider.GetRequiredService<SourcingIngredientSupplyService>(),
					provider.GetRequiredService<ResponseInvocationState>()));
		});
		var quoteId = await SeedSentQuoteAsync(host);

		await using var publisher = await RawBrokerPublisher.ConnectAsync(
			Initialize.Fixture.RabbitMqHost, Initialize.Fixture.RabbitMqPort);
		await Initialize.Fixture.WaitUntilAsync(host, async _ =>
		{
			try
			{
				await publisher.GetQueueDepthAsync(ResponseQueue);
				return true;
			}
			catch
			{
				return false;
			}
		});

		var dlqName = $"{IngredientQuoteResponseReceivedEventTopicName}.dlq";
		var initialDlqDepth = await publisher.GetQueueDepthAsync(dlqName);
		var messageId = Guid.NewGuid().ToString("N");
		var payload = new IngredientQuoteResponseReceivedEvent(
			QuoteId: quoteId,
			SupplierKey: supplierKey,
			Accepted: false,
			RawResponseJson: null,
			Reason: reason);

		await publisher.PublishEnvelopeAsync(IngredientQuoteResponseReceivedEventTopicName, messageId, payload);
		await Initialize.Fixture.WaitUntilAsync(host, async _ =>
			await publisher.GetQueueDepthAsync(dlqName) > initialDlqDepth);

		await using var scope = host.Services.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<BurcinDatabaseDbContext>();
		var quote = await db.IngredientQuotes.AsNoTracking().SingleAsync(q => q.Id == quoteId);
		var inboxCount = await db.Database
			.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM dbo.Inbox WHERE [MessageId] = {0}", messageId)
			.SingleAsync();

		Assert.AreEqual(1, invocationState.InvocationCount, "A permanently invalid response must reject without retry.");
		Assert.AreEqual(IngredientQuoteStatus.Sent, quote.Status, "Invalid response data must be rejected before mutation.");
		Assert.IsNull(quote.ResponseReceivedAt);
		Assert.AreEqual(0, inboxCount, "Reject rolls back the atomic Inbox claim so a deliberate DLQ replay remains possible.");

		await host.StopAsync();
	}

	[TestMethod]
	public async Task Subscriber_MalformedEnvelope_RoutedToDlq_NoIngredientQuoteMutation()
	{
		using var stub = new StubSupplierHandler(HttpStatusCode.OK);
		using var host = await Initialize.Fixture.BuildHostAsync(stub);
		var quoteId = await SeedSentQuoteAsync(host);

		await using var publisher = await RawBrokerPublisher.ConnectAsync(
			Initialize.Fixture.RabbitMqHost, Initialize.Fixture.RabbitMqPort);

		// Wait for the subscriber to declare its queue + DLX (so QueueDeclarePassive succeeds below).
		await Initialize.Fixture.WaitUntilAsync(host, async _ =>
		{
			try
			{
				await publisher.GetQueueDepthAsync(ResponseQueue);
				return true;
			}
			catch
			{
				return false;
			}
		});

		var dlqName = $"{IngredientQuoteResponseReceivedEventTopicName}.dlq";
		var initialDlqDepth = await publisher.GetQueueDepthAsync(dlqName);

		// Act — drop a flagrantly invalid JSON body onto the topic. Subscriber will fail to deserialize,
		// reject without requeue, broker routes to DLX → DLQ.
		var poison = Encoding.UTF8.GetBytes("{ this is not valid envelope json");
		await publisher.PublishRawAsync(IngredientQuoteResponseReceivedEventTopicName, poison);

		// Wait for the message to land in DLQ.
		await Initialize.Fixture.WaitUntilAsync(host, async _ =>
		{
			var depth = await publisher.GetQueueDepthAsync(dlqName);
			return depth > initialDlqDepth;
		});

		// Assert — DLQ gained exactly 1 message; main queue is empty (no redelivery loop); IngredientQuote untouched.
		var dlqDepth = await publisher.GetQueueDepthAsync(dlqName);
		Assert.AreEqual(initialDlqDepth + 1, dlqDepth, "Expected exactly one additional DLQ message after rejection.");

		var mainDepth = await publisher.GetQueueDepthAsync(ResponseQueue);
		Assert.AreEqual(0u, mainDepth, "Main queue must be empty — no infinite redelivery (regression for Ruya bug #6).");

		await using var scope = host.Services.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<BurcinDatabaseDbContext>();
		var quote = await db.IngredientQuotes.AsNoTracking().SingleAsync(q => q.Id == quoteId);
		Assert.AreEqual(IngredientQuoteStatus.Sent, quote.Status, "IngredientQuote must NOT mutate on poison input.");
		var inboxCount = await db.Database
			.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM dbo.Inbox").SingleAsync();
		Assert.AreEqual(0, inboxCount, "No Inbox row should be created for a poison message.");

		await host.StopAsync();
	}

	[TestMethod]
	public async Task Subscriber_PascalCaseEnvelope_DeserializesAndProcesses()
	{
		using var stub = new StubSupplierHandler(HttpStatusCode.OK);
		using var host = await Initialize.Fixture.BuildHostAsync(stub);
		var quoteId = await SeedSentQuoteAsync(host);

		await using var publisher = await RawBrokerPublisher.ConnectAsync(
			Initialize.Fixture.RabbitMqHost, Initialize.Fixture.RabbitMqPort);

		var messageId = Guid.NewGuid().ToString("N");
		var payload = new IngredientQuoteResponseReceivedEvent(
			QuoteId: quoteId,
			SupplierKey: "test-supplier",
			Accepted: true,
			RawResponseJson: """{"ok":true}""",
			Reason: null);

		// Act — publish the envelope serialized with PascalCase property names. The pre-split webhook adapter
		// used to do this before the Gateway Webhook adapter adopted camelCase. The consumer remains tolerant on read
		// so already-published envelopes can drain safely during an upgrade.
		var pascalEnvelope = new
		{
			MessageId = messageId,
			MessageType = IngredientQuoteResponseReceivedEventTopicName,
			Timestamp = DateTimeOffset.UtcNow,
			Source = "test",
			Persistent = true,
			Payload = payload,
		};
		var pascalJson = JsonSerializer.Serialize(pascalEnvelope, new JsonSerializerOptions { PropertyNamingPolicy = null });
		await publisher.PublishRawAsync(IngredientQuoteResponseReceivedEventTopicName, Encoding.UTF8.GetBytes(pascalJson), messageId);

		// Assert — handler ran, status flipped, Inbox recorded.
		await Initialize.Fixture.WaitUntilAsync(host, async sp =>
		{
			var db = sp.GetRequiredService<BurcinDatabaseDbContext>();
			var q = await db.IngredientQuotes.AsNoTracking().SingleAsync();
			return q.Status == IngredientQuoteStatus.ResponseReceived;
		});

		await using var scope = host.Services.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<BurcinDatabaseDbContext>();
		var quote = await db.IngredientQuotes.AsNoTracking().SingleAsync();
		Assert.AreEqual(IngredientQuoteStatus.ResponseReceived, quote.Status);
		Assert.IsNotNull(quote.ResponseReceivedAt);

		await host.StopAsync();
	}

	[TestMethod]
	public async Task Subscribers_HostStops_DisposeBrokerConsumers()
	{
		using var stub = new StubSupplierHandler(HttpStatusCode.OK);
		using var host = await Initialize.Fixture.BuildHostAsync(stub);
		await using var publisher = await RawBrokerPublisher.ConnectAsync(
			Initialize.Fixture.RabbitMqHost, Initialize.Fixture.RabbitMqPort);

		await Initialize.Fixture.WaitUntilAsync(host, async _ =>
		{
			var requestConsumers = await publisher.GetQueueConsumerCountAsync(RequestQueue);
			var responseConsumers = await publisher.GetQueueConsumerCountAsync(ResponseQueue);
			return requestConsumers == 1 && responseConsumers == 1;
		});

		await host.StopAsync();

		await Initialize.Fixture.WaitUntilAsync(host, async _ =>
		{
			var requestConsumers = await publisher.GetQueueConsumerCountAsync(RequestQueue);
			var responseConsumers = await publisher.GetQueueConsumerCountAsync(ResponseQueue);
			return requestConsumers == 0 && responseConsumers == 0;
		});

		Assert.AreEqual(0u, await publisher.GetQueueConsumerCountAsync(RequestQueue));
		Assert.AreEqual(0u, await publisher.GetQueueConsumerCountAsync(ResponseQueue));
	}

	private static async Task<long> SeedSentQuoteAsync(Microsoft.Extensions.Hosting.IHost host)
	{
		await using var scope = host.Services.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<BurcinDatabaseDbContext>();
		var quote = new BurcinCo.BurcinApp.Models.BurcinDatabase.IngredientQuote
		{
			SupplierKey = "test-supplier",
			IngredientsJson = "[]",
			Status = IngredientQuoteStatus.Sent,
			RequestedAt = DateTime.UtcNow,
			SentAt = DateTime.UtcNow,
		};
		db.IngredientQuotes.Add(quote);
		await db.SaveChangesAsync();
		return quote.Id;
	}
}
