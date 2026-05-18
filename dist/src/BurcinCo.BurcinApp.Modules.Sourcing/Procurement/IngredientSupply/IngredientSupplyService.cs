using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BurcinCo.BurcinApp.Data;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Events;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Interfaces;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Requests;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Responses;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ruya.Services.ReliableMessaging.Outbox;
using IngredientQuoteEntity = BurcinCo.BurcinApp.Models.BurcinDatabase.IngredientQuote;
using IngredientQuoteStatus = BurcinCo.BurcinApp.Models.BurcinDatabase.IngredientQuoteStatus;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply;

/// <summary>
/// Producer side of the Sourcing flow. <see cref="RequestQuoteAsync"/> writes an
/// <see cref="IngredientQuoteEntity"/> row + an Outbox event in the same transaction;
/// the OutboxProcessor → MessageQueueOutboundDispatcher delivers the event to RabbitMQ; and the
/// <see cref="Workers.QuoteRequestDispatcher"/> background service picks it up and makes the
/// actual HTTP call to the external supplier via <see cref="Clients.SupplierWebhookClient"/>.
/// </summary>
internal sealed partial class IngredientSupplyService : IIngredientSupplyService, ISourcingService
{
	private static readonly ActivitySource _activitySource = new(Constants.Activities.ActivitySourceName);

	private readonly BurcinDatabaseDbContext _db;
	private readonly IOutboxPublisher<BurcinDatabaseDbContext> _outbox;
	private readonly TimeProvider _timeProvider;
	private readonly ILogger<IngredientSupplyService> _logger;

	private readonly Counter<long> _requested;

	public IngredientSupplyService(
		BurcinDatabaseDbContext db,
		IOutboxPublisher<BurcinDatabaseDbContext> outbox,
		TimeProvider timeProvider,
		IMeterFactory meterFactory,
		ILogger<IngredientSupplyService> logger)
	{
		ArgumentNullException.ThrowIfNull(db);
		ArgumentNullException.ThrowIfNull(outbox);
		ArgumentNullException.ThrowIfNull(timeProvider);
		ArgumentNullException.ThrowIfNull(meterFactory);
		ArgumentNullException.ThrowIfNull(logger);
		_db = db;
		_outbox = outbox;
		_timeProvider = timeProvider;
		_logger = logger;

		var meter = meterFactory.Create(Constants.Metrics.MeterName);
		_requested = meter.CreateCounter<long>(Constants.Metrics.QuoteRequested, unit: "{quote}");
	}

	public async Task<IngredientQuoteView> RequestQuoteAsync(RequestQuoteRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		using var activity = _activitySource.StartActivity(nameof(RequestQuoteAsync));
		activity?.SetTag(Constants.Tags.SupplierKey, request.SupplierKey);

		var now = _timeProvider.GetUtcNow().UtcDateTime;
		var entity = new IngredientQuoteEntity
		{
			RecipeId = request.RecipeId,
			SupplierKey = request.SupplierKey,
			IngredientsJson = JsonSerializer.Serialize(request.Ingredients),
			Status = IngredientQuoteStatus.Pending,
			RequestedAt = now,
		};

		// Two-phase save inside an explicit transaction (mirroring the original RecipeService pattern):
		// phase 1 saves the row so EF reads back the auto-Id; phase 2 enqueues the outbox event with
		// that Id and lets the SaveChangesInterceptor flush it. The transaction wraps both saves so a
		// phase-2 failure rolls phase 1 back, preserving outbox atomicity.
		var strategy = _db.Database.CreateExecutionStrategy();
		await strategy.ExecuteAsync(async ct =>
		{
			await using var tx = await _db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

			_db.IngredientQuotes.Add(entity);
			await _db.SaveChangesAsync(ct).ConfigureAwait(false);

			var ev = new IngredientQuoteRequestedEvent(
				entity.Id,
				entity.RecipeId,
				entity.SupplierKey,
				request.Ingredients.ToList(),
				entity.RequestedAt);

			await _outbox.EnqueueAsync(
				BurcinCo.BurcinApp.Modules.Sourcing.Constants.Topics.IngredientQuoteRequested,
				ev,
				cancellationToken: ct).ConfigureAwait(false);
			await _db.SaveChangesAsync(ct).ConfigureAwait(false);

			await tx.CommitAsync(ct).ConfigureAwait(false);
		}, cancellationToken).ConfigureAwait(false);

		activity?.SetTag(Constants.Tags.QuoteId, entity.Id);
		_requested.Add(1,
			new KeyValuePair<string, object?>(Constants.Tags.QuoteId, entity.Id),
			new KeyValuePair<string, object?>(Constants.Tags.SupplierKey, entity.SupplierKey));
		LogQuoteRequested(entity.Id, entity.SupplierKey);

		return ToView(entity);
	}

	public async Task<IngredientQuoteView?> GetByIdAsync(long quoteId, CancellationToken cancellationToken = default)
	{
		using var activity = _activitySource.StartActivity(nameof(GetByIdAsync));
		activity?.SetTag(Constants.Tags.QuoteId, quoteId);
		var entity = await _db.IngredientQuotes.AsNoTracking()
			.SingleOrDefaultAsync(q => q.Id == quoteId, cancellationToken).ConfigureAwait(false);
		return entity is null ? null : ToView(entity);
	}

	private static IngredientQuoteView ToView(IngredientQuoteEntity e) =>
		new(e.Id, e.RecipeId, e.SupplierKey, e.Status, e.RequestedAt, e.SentAt, e.ResponseReceivedAt, e.ResponseJson, e.FailureReason);

	[LoggerMessage(EventId = 5001, Level = LogLevel.Information, Message = "Quote requested. Id={QuoteId} SupplierKey={SupplierKey}")]
	private partial void LogQuoteRequested(long quoteId, string supplierKey);
}
