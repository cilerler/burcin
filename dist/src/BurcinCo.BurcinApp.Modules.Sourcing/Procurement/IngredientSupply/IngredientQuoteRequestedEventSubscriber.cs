using System;
using System.Threading;
using System.Threading.Tasks;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Abstractions.Events;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Configuration;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Contracts;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ruya.Services.MessageQueue.Abstractions;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply;

/// <summary>
/// Subscribes to the internal broker topic written by the Outbox dispatcher, then delegates each
/// <see cref="IngredientQuoteRequestedEvent"/> delivery to a scoped <see cref="IIngredientSupply"/>.
/// </summary>
internal sealed partial class IngredientQuoteRequestedEventSubscriber : BackgroundService
{
	private readonly ILogger<IngredientQuoteRequestedEventSubscriber> _logger;
	private readonly IngredientSupplySettings _settings;
	private readonly IMessageQueueFactory _queueFactory;
	private readonly IServiceScopeFactory _scopeFactory;

	public IngredientQuoteRequestedEventSubscriber(
		ILogger<IngredientQuoteRequestedEventSubscriber> logger,
		IOptions<IngredientSupplySettings> options,
		IMessageQueueFactory queueFactory,
		IServiceScopeFactory scopeFactory)
	{
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(queueFactory);
		ArgumentNullException.ThrowIfNull(scopeFactory);
		_logger = logger;
		_settings = options.Value;
		_queueFactory = queueFactory;
		_scopeFactory = scopeFactory;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var topic = _settings.IngredientQuoteRequestedEventTopicName;
		var queue = await _queueFactory
			.CreateQueueAsync(_settings.MessageQueueProviderName, stoppingToken)
			.ConfigureAwait(false);
		LogStarted(topic, _settings.MessageQueueProviderName);

		await using var subscription = await queue.SubscribeAsync<IngredientQuoteRequestedEvent>(
			topic,
			HandleAsync,
			CreateSubscribeOptions(),
			stoppingToken).ConfigureAwait(false);

		try
		{
			await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
		{
			LogStopped(topic);
		}
	}

	private async Task<MessageResult> HandleAsync(MessageContext<IngredientQuoteRequestedEvent> context)
	{
		await using var scope = _scopeFactory.CreateAsyncScope();
		var service = scope.ServiceProvider.GetRequiredService<IIngredientSupply>();

		try
		{
			await service.ProcessAsync(context.Envelope.Payload, context.CancellationToken).ConfigureAwait(false);
			return MessageResult.Success();
		}
		catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (InvalidIngredientQuoteMessageException exception)
		{
			LogPermanentFailure(exception, context.Envelope.Payload.QuoteId);
			return MessageResult.Reject("The quote request is permanently invalid.");
		}
		catch (TransientSupplierException exception)
		{
			LogTransientFailure(exception, context.Envelope.Payload.QuoteId);
			return MessageResult.Retry("The supplier request failed transiently.");
		}
	}

	private SubscribeOptions CreateSubscribeOptions() => new()
	{
		MaxDeliveryCount = _settings.MaximumDeliveryCount,
		RequeueOnException = false,
		RetryPolicy = new RetryPolicy
		{
			MaxRetryAttempts = _settings.MaximumDeliveryCount - 1,
			InitialDelay = _settings.InitialRetryDelay,
			MaxDelay = _settings.MaximumRetryDelay,
			BackoffMultiplier = 2,
			UseExponentialBackoff = true,
			UseJitter = true,
		},
	};

	[LoggerMessage(EventId = 5301, Level = LogLevel.Information, Message = "IngredientQuoteRequestedEventSubscriber subscribed to topic '{Topic}' through provider '{ProviderName}'.")]
	private partial void LogStarted(string topic, string providerName);

	[LoggerMessage(EventId = 5303, Level = LogLevel.Information, Message = "IngredientQuoteRequestedEventSubscriber stopped subscription to topic '{Topic}'.")]
	private partial void LogStopped(string topic);

	[LoggerMessage(EventId = 5304, Level = LogLevel.Warning, Message = "Rejecting permanently invalid IngredientQuoteRequestedEvent for quote {QuoteId}.")]
	private partial void LogPermanentFailure(Exception exception, long quoteId);

	[LoggerMessage(EventId = 5305, Level = LogLevel.Warning, Message = "Retrying transient IngredientQuoteRequestedEvent failure for quote {QuoteId}.")]
	private partial void LogTransientFailure(Exception exception, long quoteId);
}
