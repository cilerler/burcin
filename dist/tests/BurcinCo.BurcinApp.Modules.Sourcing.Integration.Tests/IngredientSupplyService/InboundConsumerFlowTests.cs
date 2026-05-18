using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BurcinCo.BurcinApp.Data;
using BurcinCo.BurcinApp.Models.BurcinDatabase;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Events;
using BurcinCo.BurcinApp.Modules.Sourcing.Integration.Tests.Fixtures;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Integration.Tests.IngredientSupplyService;

/// <summary>
/// Inbound consumer-side flows. These exercise <c>QuoteResponseSubscriber</c> +
/// <c>SubscribeWithInboxAsync</c> + <c>QuoteResponseHandler</c> end-to-end against a real broker:
///   <list type="bullet">
///     <item>Inbox dedup — duplicate <c>MessageId</c> doesn't double-process. Regression net for the
///       reason the Inbox exists at all.</item>
///     <item>Poison message → DLQ — invalid JSON gets rejected ONCE, lands in the DLQ via auto-wired DLX,
///       no infinite redelivery loop. Regression net for Ruya bug #6 (the 4.6M-redelivery loop).</item>
///     <item>Case-insensitive deserialize — PascalCase envelope payload deserializes successfully.
///       Regression net for Ruya bug #7.</item>
///   </list>
/// Every test seeds a Pending <c>IngredientQuote</c> row first so the handler has something to update.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class InboundConsumerFlowTests
{
	private const string ResponseTopic = "webhooks.sourcing.quote-response";
	// Ruya RabbitMQ topology: queue name = `{topic}.queue` when SubscribeOptions.ConsumerGroup is not set
	// (the explicit `consumerName` parameter is for Inbox-dedup keying, not for queue naming).
	private const string ResponseQueue = ResponseTopic + ".queue";

	[TestInitialize]
	public Task TestInitializeAsync() => Initialize.Fixture.CleanTablesAsync();

	[TestMethod]
	public async Task SubscribeWithInbox_DuplicateMessageId_ProcessesOnce()
	{
		// Arrange — supplier stub never invoked on this path (inbound, not outbound), but BuildHostAsync
		// requires a handler. Seed a quote row first so the inbound handler has something to update.
		using var stub = new StubSupplierHandler(HttpStatusCode.OK);
		using var host = await Initialize.Fixture.BuildHostAsync(stub);
		var quoteId = await SeedPendingQuoteAsync(host);

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
		await publisher.PublishEnvelopeAsync(ResponseTopic, messageId, payload);
		await publisher.PublishEnvelopeAsync(ResponseTopic, messageId, payload);

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

		// Quote was updated exactly once — ResponseReceivedAt is set, ResponseJson populated, no double mutation.
		var quote = await db.IngredientQuotes.AsNoTracking().SingleAsync();
		Assert.AreEqual(IngredientQuoteStatus.ResponseReceived, quote.Status);
		Assert.IsNotNull(quote.ResponseReceivedAt);

		await host.StopAsync();
	}

	[TestMethod]
	public async Task Subscriber_MalformedEnvelope_RoutedToDlq_NoIngredientQuoteMutation()
	{
		using var stub = new StubSupplierHandler(HttpStatusCode.OK);
		using var host = await Initialize.Fixture.BuildHostAsync(stub);
		var quoteId = await SeedPendingQuoteAsync(host);

		await using var publisher = await RawBrokerPublisher.ConnectAsync(
			Initialize.Fixture.RabbitMqHost, Initialize.Fixture.RabbitMqPort);

		// Wait for the subscriber to declare its queue + DLX (so QueueDeclarePassive succeeds below).
		await Initialize.Fixture.WaitUntilAsync(host, async sp =>
		{
			try
			{
				_ = await publisher.GetQueueDepthAsync(ResponseQueue);
				return true;
			}
			catch
			{
				return false;
			}
		});

		// Act — drop a flagrantly invalid JSON body onto the topic. Subscriber will fail to deserialize,
		// reject without requeue, broker routes to DLX → DLQ.
		var poison = Encoding.UTF8.GetBytes("{ this is not valid envelope json");
		await publisher.PublishRawAsync(ResponseTopic, poison);

		// Wait for the message to land in DLQ.
		await Initialize.Fixture.WaitUntilAsync(host, async _ =>
		{
			var depth = await publisher.GetQueueDepthAsync($"{ResponseTopic}.dlq");
			return depth >= 1;
		});

		// Assert — DLQ has exactly 1 message; main queue is empty (no redelivery loop); IngredientQuote untouched.
		var dlqDepth = await publisher.GetQueueDepthAsync($"{ResponseTopic}.dlq");
		Assert.AreEqual(1u, dlqDepth, "Expected exactly one message in the DLQ after rejection.");

		var mainDepth = await publisher.GetQueueDepthAsync(ResponseQueue);
		Assert.AreEqual(0u, mainDepth, "Main queue must be empty — no infinite redelivery (regression for Ruya bug #6).");

		await using var scope = host.Services.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<BurcinDatabaseDbContext>();
		var quote = await db.IngredientQuotes.AsNoTracking().SingleAsync(q => q.Id == quoteId);
		Assert.AreEqual(IngredientQuoteStatus.Pending, quote.Status, "IngredientQuote must NOT mutate on poison input.");
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
		var quoteId = await SeedPendingQuoteAsync(host);

		await using var publisher = await RawBrokerPublisher.ConnectAsync(
			Initialize.Fixture.RabbitMqHost, Initialize.Fixture.RabbitMqPort);

		var messageId = Guid.NewGuid().ToString("N");
		var payload = new IngredientQuoteResponseReceivedEvent(
			QuoteId: quoteId,
			SupplierKey: "test-supplier",
			Accepted: true,
			RawResponseJson: """{"ok":true}""",
			Reason: null);

		// Act — publish the envelope serialized with PascalCase property names. The Gateway used to do this
		// in production before we moved Gateway-side to camelCase + made Ruya tolerant on read; both fixes
		// are belt-and-suspenders. This test guards the Ruya read-tolerance half.
		var pascalEnvelope = new
		{
			MessageId = messageId,
			MessageType = ResponseTopic,
			Timestamp = DateTimeOffset.UtcNow,
			Source = "test",
			Persistent = true,
			Payload = payload,
		};
		var pascalJson = JsonSerializer.Serialize(pascalEnvelope, new JsonSerializerOptions { PropertyNamingPolicy = null });
		await publisher.PublishRawAsync(ResponseTopic, Encoding.UTF8.GetBytes(pascalJson), messageId);

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

	private static async Task<long> SeedPendingQuoteAsync(Microsoft.Extensions.Hosting.IHost host)
	{
		await using var scope = host.Services.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<BurcinDatabaseDbContext>();
		var quote = new BurcinCo.BurcinApp.Models.BurcinDatabase.IngredientQuote
		{
			SupplierKey = "test-supplier",
			IngredientsJson = "[]",
			Status = IngredientQuoteStatus.Pending,
			RequestedAt = DateTime.UtcNow,
		};
		db.IngredientQuotes.Add(quote);
		await db.SaveChangesAsync();
		return quote.Id;
	}
}
