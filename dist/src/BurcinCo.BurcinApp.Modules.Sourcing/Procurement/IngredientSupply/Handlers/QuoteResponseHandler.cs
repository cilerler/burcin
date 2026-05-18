using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BurcinCo.BurcinApp.Data;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IngredientQuoteEntity = BurcinCo.BurcinApp.Models.BurcinDatabase.IngredientQuote;
using IngredientQuoteStatus = BurcinCo.BurcinApp.Models.BurcinDatabase.IngredientQuoteStatus;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Handlers;

/// <summary>
/// Inbox handler for inbound supplier webhook responses. Invoked once per deduplicated message
/// (the Inbox stops duplicates from re-running the handler) by <see cref="Workers.QuoteResponseSubscriber"/>.
/// Updates the matching <see cref="IngredientQuoteEntity"/> row to <see cref="IngredientQuoteStatus.ResponseReceived"/>
/// (or <see cref="IngredientQuoteStatus.Failed"/> when the supplier reported a non-acceptance).
/// </summary>
internal sealed partial class QuoteResponseHandler
{
	private static readonly ActivitySource _activitySource = new(Constants.Activities.ActivitySourceName);

	private readonly BurcinDatabaseDbContext _db;
	private readonly TimeProvider _timeProvider;
	private readonly ILogger<QuoteResponseHandler> _logger;

	private readonly Counter<long> _received;

	public QuoteResponseHandler(
		BurcinDatabaseDbContext db,
		TimeProvider timeProvider,
		IMeterFactory meterFactory,
		ILogger<QuoteResponseHandler> logger)
	{
		ArgumentNullException.ThrowIfNull(db);
		ArgumentNullException.ThrowIfNull(timeProvider);
		ArgumentNullException.ThrowIfNull(meterFactory);
		ArgumentNullException.ThrowIfNull(logger);
		_db = db;
		_timeProvider = timeProvider;
		_logger = logger;

		var meter = meterFactory.Create(Constants.Metrics.MeterName);
		_received = meter.CreateCounter<long>(Constants.Metrics.QuoteResponseReceived, unit: "{quote}");
	}

	public async Task HandleAsync(IngredientQuoteResponseReceivedEvent ev, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(ev);
		using var activity = _activitySource.StartActivity(nameof(HandleAsync));
		activity?.SetTag(Constants.Tags.QuoteId, ev.QuoteId);
		activity?.SetTag(Constants.Tags.SupplierKey, ev.SupplierKey);

		var quote = await _db.IngredientQuotes.SingleOrDefaultAsync(q => q.Id == ev.QuoteId, cancellationToken).ConfigureAwait(false);
		if (quote is null)
		{
			LogQuoteNotFound(ev.QuoteId);
			return;
		}

		quote.Status = ev.Accepted ? IngredientQuoteStatus.ResponseReceived : IngredientQuoteStatus.Failed;
		quote.ResponseReceivedAt = _timeProvider.GetUtcNow().UtcDateTime;
		quote.ResponseJson = ev.RawResponseJson ?? JsonSerializer.Serialize(ev);
		if (!ev.Accepted)
		{
			quote.FailureReason = ev.Reason;
		}

		await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		_received.Add(1,
			new KeyValuePair<string, object?>(Constants.Tags.QuoteId, ev.QuoteId),
			new KeyValuePair<string, object?>(Constants.Tags.SupplierKey, ev.SupplierKey));
		LogQuoteResponseProcessed(ev.QuoteId, ev.Accepted);
	}

	[LoggerMessage(EventId = 5201, Level = LogLevel.Warning, Message = "Quote response received but quote {QuoteId} was not found locally; possible mis-routed webhook.")]
	private partial void LogQuoteNotFound(long quoteId);

	[LoggerMessage(EventId = 5202, Level = LogLevel.Information, Message = "Quote {QuoteId} response processed. Accepted={Accepted}")]
	private partial void LogQuoteResponseProcessed(long quoteId, bool accepted);
}
