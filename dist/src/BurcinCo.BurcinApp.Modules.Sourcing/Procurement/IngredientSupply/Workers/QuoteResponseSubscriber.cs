using System;
using System.Threading;
using System.Threading.Tasks;
using BurcinCo.BurcinApp.Data;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Events;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Configuration;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ruya.Services.MessageQueue.Abstractions;
using Ruya.Services.ReliableMessaging.MessageQueue;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Workers;

/// <summary>
/// Inbound subscriber. Listens on the broker for messages the <c>Gateway</c> publishes when an
/// external supplier POSTs to <c>/webhooks/sourcing/quote-response</c> — the Gateway uses routing key
/// <see cref="BurcinCo.BurcinApp.Modules.Sourcing.Constants.Topics.IngredientQuoteResponseReceivedFromGateway"/>.
/// Uses <see cref="MessageQueueSubscribeWithInboxExtensions.SubscribeWithInboxAsync{TMessage,TDbContext}"/> for
/// idempotent delivery: duplicate messages return Success without invoking the handler twice.
/// </summary>
internal sealed partial class QuoteResponseSubscriber : BackgroundService
{
	private readonly IMessageQueueFactory _queueFactory;
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly IngredientSupplySettings _settings;
	private readonly ILogger<QuoteResponseSubscriber> _logger;

	public QuoteResponseSubscriber(
		IMessageQueueFactory queueFactory,
		IServiceScopeFactory scopeFactory,
		IOptions<IngredientSupplySettings> settings,
		ILogger<QuoteResponseSubscriber> logger)
	{
		ArgumentNullException.ThrowIfNull(queueFactory);
		ArgumentNullException.ThrowIfNull(scopeFactory);
		ArgumentNullException.ThrowIfNull(settings);
		ArgumentNullException.ThrowIfNull(logger);
		_queueFactory = queueFactory;
		_scopeFactory = scopeFactory;
		_settings = settings.Value;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var queue = await _queueFactory.CreateQueueAsync(_settings.MessageQueueProviderName, stoppingToken).ConfigureAwait(false);
		LogStarted(BurcinCo.BurcinApp.Modules.Sourcing.Constants.Topics.IngredientQuoteResponseReceivedFromGateway, Constants.ResponseConsumerName);

		await queue.SubscribeWithInboxAsync<IngredientQuoteResponseReceivedEvent, BurcinDatabaseDbContext>(
			topic: BurcinCo.BurcinApp.Modules.Sourcing.Constants.Topics.IngredientQuoteResponseReceivedFromGateway,
			consumerName: Constants.ResponseConsumerName,
			scopeFactory: _scopeFactory,
			handler: HandleAsync,
			options: null,
			cancellationToken: stoppingToken).ConfigureAwait(false);

		try
		{
			await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
		}
		catch (TaskCanceledException) { /* normal shutdown */ }
	}

	private async Task<MessageResult> HandleAsync(MessageContext<IngredientQuoteResponseReceivedEvent> context)
	{
		try
		{
			using var scope = _scopeFactory.CreateScope();
			var handler = scope.ServiceProvider.GetRequiredService<QuoteResponseHandler>();
			await handler.HandleAsync(context.Envelope.Payload, context.CancellationToken).ConfigureAwait(false);
			return MessageResult.Success();
		}
		catch (Exception ex)
		{
			LogHandlerFailure(ex, context.Envelope.Payload.QuoteId);
			return MessageResult.Retry(ex.Message);
		}
	}

	[LoggerMessage(EventId = 5401, Level = LogLevel.Information, Message = "QuoteResponseSubscriber subscribed (topic='{Topic}', consumer='{Consumer}').")]
	private partial void LogStarted(string topic, string consumer);

	[LoggerMessage(EventId = 5402, Level = LogLevel.Error, Message = "QuoteResponseSubscriber handler failed for Quote {QuoteId}.")]
	private partial void LogHandlerFailure(Exception exception, long quoteId);
}
