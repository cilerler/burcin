using System;
using System.Threading;
using System.Threading.Tasks;
using BurcinCo.BurcinApp.Data;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Events;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Clients;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ruya.Services.MessageQueue.Abstractions;
using IngredientQuoteStatus = BurcinCo.BurcinApp.Models.BurcinDatabase.IngredientQuoteStatus;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Workers;

/// <summary>
/// Outbound dispatcher worker. Subscribes to the internal broker topic that the Outbox
/// dispatcher writes to (<see cref="BurcinCo.BurcinApp.Modules.Sourcing.Constants.Topics.IngredientQuoteRequested"/>),
/// then invokes <see cref="SupplierWebhookClient"/> to make the actual external HTTP call.
/// On success the corresponding <c>IngredientQuote</c> row transitions to
/// <see cref="IngredientQuoteStatus.Sent"/>; on failure it transitions to
/// <see cref="IngredientQuoteStatus.Failed"/> with a captured reason.
/// </summary>
internal sealed partial class QuoteRequestDispatcher : BackgroundService
{
	private readonly IMessageQueueFactory _queueFactory;
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly IngredientSupplySettings _settings;
	private readonly TimeProvider _timeProvider;
	private readonly ILogger<QuoteRequestDispatcher> _logger;

	public QuoteRequestDispatcher(
		IMessageQueueFactory queueFactory,
		IServiceScopeFactory scopeFactory,
		IOptions<IngredientSupplySettings> settings,
		TimeProvider timeProvider,
		ILogger<QuoteRequestDispatcher> logger)
	{
		ArgumentNullException.ThrowIfNull(queueFactory);
		ArgumentNullException.ThrowIfNull(scopeFactory);
		ArgumentNullException.ThrowIfNull(settings);
		ArgumentNullException.ThrowIfNull(timeProvider);
		ArgumentNullException.ThrowIfNull(logger);
		_queueFactory = queueFactory;
		_scopeFactory = scopeFactory;
		_settings = settings.Value;
		_timeProvider = timeProvider;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var queue = await _queueFactory.CreateQueueAsync(_settings.MessageQueueProviderName, stoppingToken).ConfigureAwait(false);
		LogStarted(BurcinCo.BurcinApp.Modules.Sourcing.Constants.Topics.IngredientQuoteRequested);

		await queue.SubscribeAsync<IngredientQuoteRequestedEvent>(
			BurcinCo.BurcinApp.Modules.Sourcing.Constants.Topics.IngredientQuoteRequested,
			HandleAsync,
			options: null,
			cancellationToken: stoppingToken).ConfigureAwait(false);

		// Hold the worker open until cancellation; subscription is cancelled by the token.
		try
		{
			await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
		}
		catch (TaskCanceledException) { /* normal shutdown */ }
	}

	private async Task<MessageResult> HandleAsync(MessageContext<IngredientQuoteRequestedEvent> context)
	{
		var ev = context.Envelope.Payload;
		using var scope = _scopeFactory.CreateScope();
		var supplierClient = scope.ServiceProvider.GetRequiredService<SupplierWebhookClient>();
		var db = scope.ServiceProvider.GetRequiredService<BurcinDatabaseDbContext>();

		var sent = await supplierClient.PostQuoteRequestAsync(ev, context.CancellationToken).ConfigureAwait(false);

		var quote = await db.IngredientQuotes.SingleOrDefaultAsync(q => q.Id == ev.QuoteId, context.CancellationToken).ConfigureAwait(false);
		if (quote is null)
		{
			LogQuoteNotFound(ev.QuoteId);
			return MessageResult.Success(); // nothing we can recover by failing the message
		}

		var now = _timeProvider.GetUtcNow().UtcDateTime;
		if (sent)
		{
			quote.Status = IngredientQuoteStatus.Sent;
			quote.SentAt = now;
		}
		else
		{
			quote.Status = IngredientQuoteStatus.Failed;
			quote.FailureReason = "Supplier endpoint did not accept the request.";
		}
		await db.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);

		// Returning Success here means broker won't redeliver. Treat the failure as terminal in this
		// demo flow; production code might return Retry/Reject to trigger DLQ + retry instead.
		return MessageResult.Success();
	}

	[LoggerMessage(EventId = 5301, Level = LogLevel.Information, Message = "QuoteRequestDispatcher subscribed to topic '{Topic}'.")]
	private partial void LogStarted(string topic);

	[LoggerMessage(EventId = 5302, Level = LogLevel.Warning, Message = "QuoteRequestDispatcher: quote {QuoteId} not found in DB; event dropped.")]
	private partial void LogQuoteNotFound(long quoteId);
}
