using System;
using System.Threading;
using System.Threading.Tasks;
using BurcinCo.BurcinApp.Data;
using BurcinCo.BurcinApp.Models.BurcinDatabase;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Events;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Contracts;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Integration.Tests.IngredientSupplyService;

[TestClass]
public sealed class ResponseStateTransitionTests
{
	private const string SupplierKey = "test-supplier";
	private static readonly DateTime SentAt = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
	private static readonly DateTime ResponseReceivedAt = new(2026, 1, 2, 3, 5, 6, DateTimeKind.Utc);

	[TestInitialize]
	public Task TestInitializeAsync() => Initialize.Fixture.CleanTablesAsync();

	[TestMethod]
	public async Task ProcessAsync_PendingAcceptedResponse_ThrowsAndDoesNotMutate()
	{
		await using var scope = Initialize.Fixture.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<BurcinDatabaseDbContext>();
		var sut = scope.ServiceProvider.GetRequiredService<IIngredientSupply>();
		var quote = await AddQuoteAsync(db, IngredientQuoteStatus.Pending);

		var response = CreateResponse(
			quote.Id,
			accepted: true,
			rawResponseJson: """{"accepted":true}""");

		await AssertThrowsAsync<InvalidIngredientQuoteMessageException>(() =>
			sut.ProcessAsync(response, CancellationToken.None));

		var committed = await ReloadAsync(db, quote.Id);
		Assert.AreEqual(IngredientQuoteStatus.Pending, committed.Status);
		Assert.IsNull(committed.SentAt);
		Assert.IsNull(committed.ResponseReceivedAt);
		Assert.IsNull(committed.ResponseJson);
		Assert.IsNull(committed.FailureReason);
	}

	[TestMethod]
	public async Task ProcessAsync_PendingRejectedResponse_ThrowsAndDoesNotMutate()
	{
		await using var scope = Initialize.Fixture.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<BurcinDatabaseDbContext>();
		var sut = scope.ServiceProvider.GetRequiredService<IIngredientSupply>();
		var quote = await AddQuoteAsync(db, IngredientQuoteStatus.Pending);

		var response = CreateResponse(
			quote.Id,
			accepted: false,
			rawResponseJson: """{"accepted":false}""",
			reason: "out-of-order rejection");

		await AssertThrowsAsync<InvalidIngredientQuoteMessageException>(() =>
			sut.ProcessAsync(response, CancellationToken.None));

		var committed = await ReloadAsync(db, quote.Id);
		Assert.AreEqual(IngredientQuoteStatus.Pending, committed.Status);
		Assert.IsNull(committed.SentAt);
		Assert.IsNull(committed.ResponseReceivedAt);
		Assert.IsNull(committed.ResponseJson);
		Assert.IsNull(committed.FailureReason);
	}

	[TestMethod]
	public async Task ProcessAsync_SentAcceptedResponse_TransitionsToResponseReceived()
	{
		await using var scope = Initialize.Fixture.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<BurcinDatabaseDbContext>();
		var sut = scope.ServiceProvider.GetRequiredService<IIngredientSupply>();
		var quote = await AddQuoteAsync(
			db,
			IngredientQuoteStatus.Sent,
			sentAt: SentAt,
			failureReason: "stale failure text");

		var response = CreateResponse(
			quote.Id,
			accepted: true,
			rawResponseJson: """{"accepted":true}""");
		await sut.ProcessAsync(response, CancellationToken.None);

		var committed = await ReloadAsync(db, quote.Id);
		Assert.AreEqual(IngredientQuoteStatus.ResponseReceived, committed.Status);
		Assert.AreEqual<DateTime?>(SentAt, committed.SentAt);
		Assert.IsNotNull(committed.ResponseReceivedAt);
		Assert.AreEqual("""{"accepted":true}""", committed.ResponseJson);
		Assert.IsNull(committed.FailureReason, "An accepted response must clear mutually exclusive failure state.");
	}

	[TestMethod]
	public async Task ProcessAsync_SentRejectedResponse_TransitionsToFailed()
	{
		await using var scope = Initialize.Fixture.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<BurcinDatabaseDbContext>();
		var sut = scope.ServiceProvider.GetRequiredService<IIngredientSupply>();
		var quote = await AddQuoteAsync(db, IngredientQuoteStatus.Sent, sentAt: SentAt);

		var response = CreateResponse(
			quote.Id,
			accepted: false,
			rawResponseJson: """{"accepted":false}""",
			reason: "supplier rejected quote");
		await sut.ProcessAsync(response, CancellationToken.None);

		var committed = await ReloadAsync(db, quote.Id);
		Assert.AreEqual(IngredientQuoteStatus.Failed, committed.Status);
		Assert.AreEqual<DateTime?>(SentAt, committed.SentAt);
		Assert.IsNotNull(committed.ResponseReceivedAt);
		Assert.AreEqual("""{"accepted":false}""", committed.ResponseJson);
		Assert.AreEqual("supplier rejected quote", committed.FailureReason);
	}

	[TestMethod]
	public async Task ProcessAsync_ResponseReceivedAcceptedResponseFromFreshEnvelope_IsNoOp()
	{
		await using var scope = Initialize.Fixture.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<BurcinDatabaseDbContext>();
		var sut = scope.ServiceProvider.GetRequiredService<IIngredientSupply>();
		var quote = await AddQuoteAsync(
			db,
			IngredientQuoteStatus.ResponseReceived,
			sentAt: SentAt,
			responseReceivedAt: ResponseReceivedAt,
			responseJson: """{"accepted":true,"original":true}""");

		// Calling the business handler directly bypasses Inbox identity and models the same outcome
		// arriving under a fresh transport envelope ID.
		var replay = CreateResponse(
			quote.Id,
			accepted: true,
			rawResponseJson: """{"accepted":true,"duplicate":true}""");
		await sut.ProcessAsync(replay, CancellationToken.None);

		var committed = await ReloadAsync(db, quote.Id);
		AssertTerminalState(
			committed,
			IngredientQuoteStatus.ResponseReceived,
			"""{"accepted":true,"original":true}""",
			failureReason: null);
	}

	[TestMethod]
	public async Task ProcessAsync_ResponseReceivedRejectedResponse_ThrowsAndDoesNotMutate()
	{
		await using var scope = Initialize.Fixture.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<BurcinDatabaseDbContext>();
		var sut = scope.ServiceProvider.GetRequiredService<IIngredientSupply>();
		var quote = await AddQuoteAsync(
			db,
			IngredientQuoteStatus.ResponseReceived,
			sentAt: SentAt,
			responseReceivedAt: ResponseReceivedAt,
			responseJson: """{"accepted":true,"original":true}""");

		var conflicting = CreateResponse(
			quote.Id,
			accepted: false,
			rawResponseJson: """{"accepted":false}""",
			reason: "late conflicting response");
		await AssertThrowsAsync<InvalidIngredientQuoteMessageException>(() =>
			sut.ProcessAsync(conflicting, CancellationToken.None));

		var committed = await ReloadAsync(db, quote.Id);
		AssertTerminalState(
			committed,
			IngredientQuoteStatus.ResponseReceived,
			"""{"accepted":true,"original":true}""",
			failureReason: null);
	}

	[TestMethod]
	public async Task ProcessAsync_FailedRejectedResponseFromFreshEnvelope_IsNoOp()
	{
		await using var scope = Initialize.Fixture.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<BurcinDatabaseDbContext>();
		var sut = scope.ServiceProvider.GetRequiredService<IIngredientSupply>();
		var quote = await AddQuoteAsync(
			db,
			IngredientQuoteStatus.Failed,
			sentAt: SentAt,
			responseReceivedAt: ResponseReceivedAt,
			responseJson: """{"accepted":false,"original":true}""",
			failureReason: "original rejection");

		// A distinct transport envelope carrying the same terminal outcome is a business-level replay.
		var replay = CreateResponse(
			quote.Id,
			accepted: false,
			rawResponseJson: """{"accepted":false,"duplicate":true}""",
			reason: "replacement rejection");
		await sut.ProcessAsync(replay, CancellationToken.None);

		var committed = await ReloadAsync(db, quote.Id);
		AssertTerminalState(
			committed,
			IngredientQuoteStatus.Failed,
			"""{"accepted":false,"original":true}""",
			"original rejection");
	}

	[TestMethod]
	public async Task ProcessAsync_FailedAcceptedResponse_ThrowsAndDoesNotMutate()
	{
		await using var scope = Initialize.Fixture.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<BurcinDatabaseDbContext>();
		var sut = scope.ServiceProvider.GetRequiredService<IIngredientSupply>();
		var quote = await AddQuoteAsync(
			db,
			IngredientQuoteStatus.Failed,
			sentAt: SentAt,
			responseReceivedAt: ResponseReceivedAt,
			responseJson: """{"accepted":false,"original":true}""",
			failureReason: "original rejection");

		var conflicting = CreateResponse(
			quote.Id,
			accepted: true,
			rawResponseJson: """{"accepted":true}""");
		await AssertThrowsAsync<InvalidIngredientQuoteMessageException>(() =>
			sut.ProcessAsync(conflicting, CancellationToken.None));

		var committed = await ReloadAsync(db, quote.Id);
		AssertTerminalState(
			committed,
			IngredientQuoteStatus.Failed,
			"""{"accepted":false,"original":true}""",
			"original rejection");
	}

	private static async Task<IngredientQuote> AddQuoteAsync(
		BurcinDatabaseDbContext db,
		string status,
		DateTime? sentAt = null,
		DateTime? responseReceivedAt = null,
		string? responseJson = null,
		string? failureReason = null)
	{
		var quote = new IngredientQuote
		{
			SupplierKey = SupplierKey,
			IngredientsJson = "[]",
			Status = status,
			RequestedAt = SentAt.AddMinutes(-1),
			SentAt = sentAt,
			ResponseReceivedAt = responseReceivedAt,
			ResponseJson = responseJson,
			FailureReason = failureReason,
		};
		db.IngredientQuotes.Add(quote);
		await db.SaveChangesAsync();
		return quote;
	}

	private static IngredientQuoteResponseReceivedEvent CreateResponse(
		long quoteId,
		bool accepted,
		string rawResponseJson,
		string? reason = null) =>
		new(quoteId, SupplierKey, accepted, rawResponseJson, reason);

	private static async Task<IngredientQuote> ReloadAsync(BurcinDatabaseDbContext db, long quoteId)
	{
		db.ChangeTracker.Clear();
		return await db.IngredientQuotes.AsNoTracking().SingleAsync(q => q.Id == quoteId);
	}

	private static void AssertTerminalState(
		IngredientQuote quote,
		string expectedStatus,
		string expectedResponseJson,
		string? failureReason)
	{
		Assert.AreEqual(expectedStatus, quote.Status);
		Assert.AreEqual<DateTime?>(SentAt, quote.SentAt);
		Assert.AreEqual<DateTime?>(ResponseReceivedAt, quote.ResponseReceivedAt);
		Assert.AreEqual(expectedResponseJson, quote.ResponseJson);
		Assert.AreEqual(failureReason, quote.FailureReason);
	}

	private static async Task AssertThrowsAsync<TException>(Func<Task> action)
		where TException : Exception
	{
		try
		{
			await action().ConfigureAwait(false);
			Assert.Fail($"Expected {typeof(TException).Name}.");
		}
		catch (TException)
		{
			// Expected.
		}
	}
}
