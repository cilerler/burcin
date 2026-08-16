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
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Serialization;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Abstractions.Events;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Abstractions.Serialization;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Clients;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Configuration;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Contracts;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ruya.Diagnostics.DistributedTracing;
using Ruya.Services.ReliableMessaging.Outbox;
using IngredientQuoteEntity = BurcinCo.BurcinApp.Models.BurcinDatabase.IngredientQuote;
using IngredientQuoteStatus = BurcinCo.BurcinApp.Models.BurcinDatabase.IngredientQuoteStatus;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply;

/// <summary>
/// Producer side of the Sourcing flow. <see cref="RequestQuoteAsync"/> writes an
/// <see cref="IngredientQuoteEntity"/> row + an Outbox event in the same transaction;
/// the OutboxProcessor → MessageQueueOutboundDispatcher delivers the event to RabbitMQ; and the
/// root <see cref="IngredientQuoteRequestedEventSubscriber"/> background service owns the subscription and delegates
/// each delivery to this scoped business service, which calls the external supplier via
/// <see cref="SupplierWebhookClient"/>.
/// </summary>
internal sealed partial class IngredientSupplyService : IIngredientSupply, ISourcingService
{
	private readonly BurcinDatabaseDbContext _db;
	private readonly IOutboxPublisher<BurcinDatabaseDbContext> _outbox;
	private readonly IDistributedTracing _tracing;
	private readonly SupplierWebhookClient _supplierClient;
	private readonly SupplierWebhookClientSettings _supplierSettings;
	private readonly IngredientSupplySettings _settings;
	private readonly TimeProvider _timeProvider;
	private readonly ILogger<IngredientSupplyService> _logger;

	private readonly Counter<long> _requested;
	private readonly Counter<long> _sent;

	public IngredientSupplyService(
		ILogger<IngredientSupplyService> logger,
		IMeterFactory meterFactory,
		IOptions<IngredientSupplySettings> options,
		IOptions<SupplierWebhookClientSettings> supplierOptions,
		BurcinDatabaseDbContext db,
		IOutboxPublisher<BurcinDatabaseDbContext> outbox,
		IDistributedTracing tracing,
		SupplierWebhookClient supplierClient,
		TimeProvider timeProvider)
	{
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(meterFactory);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(supplierOptions);
		ArgumentNullException.ThrowIfNull(db);
		ArgumentNullException.ThrowIfNull(outbox);
		ArgumentNullException.ThrowIfNull(tracing);
		ArgumentNullException.ThrowIfNull(supplierClient);
		ArgumentNullException.ThrowIfNull(timeProvider);
		_logger = logger;
		_db = db;
		_outbox = outbox;
		_tracing = tracing;
		_supplierClient = supplierClient;
		_supplierSettings = supplierOptions.Value;
		_settings = options.Value;
		_timeProvider = timeProvider;

		var meter = meterFactory.Create(Constants.Metrics.MeterName);
		_requested = meter.CreateCounter<long>(
			Constants.Metrics.QuoteRequested,
			unit: "{quote}",
			description: "Quotes persisted with an outbox request.");
		_sent = meter.CreateCounter<long>(
			Constants.Metrics.QuoteSent,
			unit: "{quote}",
			description: "Quote requests accepted by a supplier endpoint and persisted as sent.");
	}

	public async Task<IngredientQuoteView> RequestQuoteAsync(RequestQuoteRequest request, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ValidateRequest(request);
		using var activity = _tracing.StartActivity(nameof(RequestQuoteAsync), ActivityKind.Internal);
		activity.SetTag(Constants.Tags.InternalServiceName, Constants.ServiceName);
		activity.SetTag(Constants.Tags.SupplierKey, request.SupplierKey);

		var now = _timeProvider.GetUtcNow();
		var entity = new IngredientQuoteEntity
		{
			RecipeId = request.RecipeId,
			SupplierKey = request.SupplierKey,
			IngredientsJson = JsonSerializer.Serialize(
				request.Ingredients,
				SourcingJsonSerializerContext.Default.Options),
			Status = IngredientQuoteStatus.Pending,
			RequestedAt = now.UtcDateTime,
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
				ToUtcDateTimeOffset(entity.RequestedAt));

			await _outbox.EnqueueSourceGeneratedAsync(
				_settings.IngredientQuoteRequestedEventTopicName,
				ev,
				IngredientSupplyContractJsonSerializerContext.Default.IngredientQuoteRequestedEvent,
				options: new OutboxPublishOverrides
				{
					DispatcherName = _settings.MessageQueueProviderName,
				},
				cancellationToken: ct).ConfigureAwait(false);
			await _db.SaveChangesAsync(ct).ConfigureAwait(false);

			await tx.CommitAsync(ct).ConfigureAwait(false);
		}, cancellationToken).ConfigureAwait(false);

		activity.SetTag(Constants.Tags.QuoteId, entity.Id);
		activity.SetStatus(ActivityStatusCode.Ok);
		_requested.Add(1,
			new KeyValuePair<string, object?>(Constants.Tags.HasRecipe, entity.RecipeId.HasValue));
		LogQuoteRequested(entity.Id, entity.SupplierKey);

		return ToView(entity);
	}

	public async Task<IngredientQuoteView?> GetByIdAsync(long quoteId, CancellationToken cancellationToken)
	{
		using var activity = _tracing.StartActivity(nameof(GetByIdAsync), ActivityKind.Internal);
		activity.SetTag(Constants.Tags.InternalServiceName, Constants.ServiceName);
		activity.SetTag(Constants.Tags.QuoteId, quoteId);
		var entity = await _db.IngredientQuotes.AsNoTracking()
			.SingleOrDefaultAsync(q => q.Id == quoteId, cancellationToken).ConfigureAwait(false);
		activity.SetStatus(ActivityStatusCode.Ok);
		return entity is null ? null : ToView(entity);
	}

	public async Task ProcessAsync(
		IngredientQuoteRequestedEvent message,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ValidateMessageIdentity(message.QuoteId, message.SupplierKey, nameof(IngredientQuoteRequestedEvent));

		using var activity = _tracing.StartActivity(
			$"{nameof(IngredientQuoteRequestedEvent)}.{nameof(ProcessAsync)}",
			ActivityKind.Internal);
		activity.SetTag(Constants.Tags.InternalServiceName, Constants.ServiceName);
		activity.SetTag(Constants.Tags.QuoteId, message.QuoteId);
		activity.SetTag(Constants.Tags.SupplierKey, message.SupplierKey);

		var quote = await _db.IngredientQuotes
			.SingleOrDefaultAsync(q => q.Id == message.QuoteId, cancellationToken)
			.ConfigureAwait(false);
		if (quote is null)
		{
			throw new InvalidIngredientQuoteMessageException(
				$"{nameof(IngredientQuoteRequestedEvent)} references quote {message.QuoteId}, which does not exist.");
		}
		if (!string.Equals(quote.SupplierKey, message.SupplierKey, StringComparison.Ordinal))
		{
			throw new InvalidIngredientQuoteMessageException(
				$"{nameof(IngredientQuoteRequestedEvent)} supplier '{message.SupplierKey}' does not match quote {message.QuoteId} supplier '{quote.SupplierKey}'.");
		}

		// A committed transition is the idempotency boundary for broker redelivery. The stable HTTP
		// Idempotency-Key covers the smaller window where the supplier accepted the call but the database
		// save did not commit.
		if (!string.Equals(quote.Status, IngredientQuoteStatus.Pending, StringComparison.Ordinal))
		{
			LogQuoteRequestAlreadyProcessed(message.QuoteId, quote.Status);
			activity.SetStatus(ActivityStatusCode.Ok);
			return;
		}

		// Validate the local identity and routing invariants before crossing the irreversible HTTP boundary.
		await _supplierClient.PostQuoteRequestAsync(message, cancellationToken).ConfigureAwait(false);
		quote.Status = IngredientQuoteStatus.Sent;
		quote.SentAt = _timeProvider.GetUtcNow().UtcDateTime;

		await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		_sent.Add(1,
			new KeyValuePair<string, object?>(Constants.Tags.HasRecipe, quote.RecipeId.HasValue));
		LogQuoteSent(message.QuoteId, message.SupplierKey);
		activity.SetStatus(ActivityStatusCode.Ok);
	}

	public async Task ProcessAsync(
		IngredientQuoteResponseReceivedEvent message,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ValidateMessageIdentity(message.QuoteId, message.SupplierKey, nameof(IngredientQuoteResponseReceivedEvent));
		if (!message.Accepted && string.IsNullOrWhiteSpace(message.Reason))
		{
			throw new InvalidIngredientQuoteMessageException(
				$"{nameof(IngredientQuoteResponseReceivedEvent)} must include a rejection reason when Accepted is false.");
		}

		using var activity = _tracing.StartActivity(
			$"{nameof(IngredientQuoteResponseReceivedEvent)}.{nameof(ProcessAsync)}",
			ActivityKind.Internal);
		activity.SetTag(Constants.Tags.InternalServiceName, Constants.ServiceName);
		activity.SetTag(Constants.Tags.QuoteId, message.QuoteId);
		activity.SetTag(Constants.Tags.SupplierKey, message.SupplierKey);

		var quote = await _db.IngredientQuotes
			.SingleOrDefaultAsync(q => q.Id == message.QuoteId, cancellationToken)
			.ConfigureAwait(false);
		if (quote is null)
		{
			throw new InvalidIngredientQuoteMessageException(
				$"{nameof(IngredientQuoteResponseReceivedEvent)} references quote {message.QuoteId}, which does not exist.");
		}
		if (!string.Equals(quote.SupplierKey, message.SupplierKey, StringComparison.Ordinal))
		{
			throw new InvalidIngredientQuoteMessageException(
				$"{nameof(IngredientQuoteResponseReceivedEvent)} supplier '{message.SupplierKey}' does not match quote {message.QuoteId} supplier '{quote.SupplierKey}'.");
		}

		var incomingStatus = message.Accepted
			? IngredientQuoteStatus.ResponseReceived
			: IngredientQuoteStatus.Failed;
		if (quote.Status is IngredientQuoteStatus.ResponseReceived or IngredientQuoteStatus.Failed)
		{
			if (string.Equals(quote.Status, incomingStatus, StringComparison.Ordinal))
			{
				LogQuoteResponseAlreadyProcessed(message.QuoteId, quote.Status);
				activity.SetStatus(ActivityStatusCode.Ok);
				return;
			}

			throw new InvalidIngredientQuoteMessageException(
				$"{nameof(IngredientQuoteResponseReceivedEvent)} cannot change terminal quote {message.QuoteId} from '{quote.Status}' to '{incomingStatus}'.");
		}
		if (!string.Equals(quote.Status, IngredientQuoteStatus.Sent, StringComparison.Ordinal))
		{
			throw new InvalidIngredientQuoteMessageException(
				$"{nameof(IngredientQuoteResponseReceivedEvent)} requires quote {message.QuoteId} to be '{IngredientQuoteStatus.Sent}', but it is '{quote.Status}'.");
		}

		quote.Status = incomingStatus;
		quote.ResponseReceivedAt = _timeProvider.GetUtcNow().UtcDateTime;
		quote.ResponseJson = message.RawResponseJson ?? JsonSerializer.Serialize(
			message,
			SourcingJsonSerializerContext.Default.IngredientQuoteResponseReceivedEvent);
		quote.FailureReason = message.Accepted ? null : message.Reason;

		await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		activity.SetStatus(ActivityStatusCode.Ok);
	}

	private void ValidateRequest(RequestQuoteRequest request)
	{
		var errors = new List<string>();
		if (string.IsNullOrWhiteSpace(request.SupplierKey))
		{
			errors.Add("SupplierKey is required.");
		}
		else if (!_supplierSettings.Suppliers.ContainsKey(request.SupplierKey))
		{
			errors.Add($"Supplier '{request.SupplierKey}' is not configured.");
		}
		if (request.RecipeId.HasValue && request.RecipeId.Value <= 0)
		{
			errors.Add("RecipeId must be positive when provided.");
		}
		if (request.Ingredients is null || request.Ingredients.Count == 0)
		{
			errors.Add("At least one ingredient is required.");
		}
		else
		{
			for (var index = 0; index < request.Ingredients.Count; index++)
			{
				var ingredient = request.Ingredients[index];
				if (ingredient is null || string.IsNullOrWhiteSpace(ingredient.Name))
				{
					errors.Add($"Ingredients[{index}].Name is required.");
				}
				if (ingredient is null || !float.IsFinite(ingredient.Quantity) || ingredient.Quantity <= 0)
				{
					errors.Add($"Ingredients[{index}].Quantity must be a positive finite value.");
				}
				if (ingredient is null || string.IsNullOrWhiteSpace(ingredient.Unit))
				{
					errors.Add($"Ingredients[{index}].Unit is required.");
				}
			}
		}

		if (errors.Count > 0)
		{
			throw new IngredientSupplyValidationException(errors);
		}
	}

	private static void ValidateMessageIdentity(long quoteId, string supplierKey, string eventName)
	{
		if (quoteId <= 0)
		{
			throw new InvalidIngredientQuoteMessageException(
				$"{eventName} QuoteId must be positive; received {quoteId}.");
		}
		if (string.IsNullOrWhiteSpace(supplierKey))
		{
			throw new InvalidIngredientQuoteMessageException(
				$"{eventName} SupplierKey must not be blank.");
		}
	}

	private static IngredientQuoteView ToView(IngredientQuoteEntity e) =>
		new(
			e.Id,
			e.RecipeId,
			e.SupplierKey,
			e.Status,
			ToUtcDateTimeOffset(e.RequestedAt),
			ToUtcDateTimeOffset(e.SentAt),
			ToUtcDateTimeOffset(e.ResponseReceivedAt),
			e.ResponseJson,
			e.FailureReason);

	private static DateTimeOffset ToUtcDateTimeOffset(DateTime value) =>
		new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

	private static DateTimeOffset? ToUtcDateTimeOffset(DateTime? value) =>
		value.HasValue ? ToUtcDateTimeOffset(value.Value) : null;

	[LoggerMessage(EventId = 5001, Level = LogLevel.Information, Message = "Quote requested. Id={QuoteId} SupplierKey={SupplierKey}")]
	private partial void LogQuoteRequested(long quoteId, string supplierKey);

	[LoggerMessage(EventId = 5302, Level = LogLevel.Information, Message = "Quote request {QuoteId} already reached status {Status}; skipping duplicate supplier call.")]
	private partial void LogQuoteRequestAlreadyProcessed(long quoteId, string status);

	[LoggerMessage(EventId = 5303, Level = LogLevel.Information, Message = "Quote response {QuoteId} already reached terminal status {Status}; skipping duplicate response mutation.")]
	private partial void LogQuoteResponseAlreadyProcessed(long quoteId, string status);

	[LoggerMessage(EventId = 5002, Level = LogLevel.Information, Message = "Quote {QuoteId} sent to supplier '{SupplierKey}' and committed as sent.")]
	private partial void LogQuoteSent(long quoteId, string supplierKey);
}
