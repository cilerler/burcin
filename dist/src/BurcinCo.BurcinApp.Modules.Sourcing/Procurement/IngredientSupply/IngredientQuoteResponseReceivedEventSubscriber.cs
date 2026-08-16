using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using BurcinCo.BurcinApp.Data;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Events;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Configuration;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Contracts;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ruya.Services.MessageQueue.Abstractions;
using Ruya.Services.ReliableMessaging.MessageQueue;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply;

/// <summary>
/// Subscribes to supplier responses published by the Gateway Webhook adapter and delegates each inbox-deduplicated
/// <see cref="IngredientQuoteResponseReceivedEvent"/> delivery to a scoped <see cref="IIngredientSupply"/>.
/// </summary>
internal sealed partial class IngredientQuoteResponseReceivedEventSubscriber : BackgroundService
{
	private readonly ILogger<IngredientQuoteResponseReceivedEventSubscriber> _logger;
	private readonly IngredientSupplySettings _settings;
	private readonly IMessageQueueFactory _queueFactory;
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly Counter<long> _responseReceived;
	private readonly Counter<long> _failed;

	public IngredientQuoteResponseReceivedEventSubscriber(
		ILogger<IngredientQuoteResponseReceivedEventSubscriber> logger,
		IMeterFactory meterFactory,
		IOptions<IngredientSupplySettings> options,
		IMessageQueueFactory queueFactory,
		IServiceScopeFactory scopeFactory)
	{
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(meterFactory);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(queueFactory);
		ArgumentNullException.ThrowIfNull(scopeFactory);
		_logger = logger;
		_settings = options.Value;
		_queueFactory = queueFactory;
		_scopeFactory = scopeFactory;

		var meter = meterFactory.Create(Constants.Metrics.MeterName);
		_responseReceived = meter.CreateCounter<long>(
			Constants.Metrics.QuoteResponseReceived,
			unit: "{quote}",
			description: "Supplier responses committed, including rejected quotes.");
		_failed = meter.CreateCounter<long>(
			Constants.Metrics.QuoteFailed,
			unit: "{quote}",
			description: "Quotes committed in the failed state.");
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var topic = _settings.IngredientQuoteResponseReceivedEventTopicName;
		var queue = await _queueFactory
			.CreateQueueAsync(_settings.MessageQueueProviderName, stoppingToken)
			.ConfigureAwait(false);
		LogStarted(topic, Constants.ResponseConsumerName, _settings.MessageQueueProviderName);

		await using var subscription = await queue.SubscribeWithInboxAndPostCommitAsync<IngredientQuoteResponseReceivedEvent, BurcinDatabaseDbContext>(
			topic: topic,
			consumerName: Constants.ResponseConsumerName,
			scopeFactory: _scopeFactory,
			handler: HandleAsync,
			postCommitObserver: ObserveCommittedAsync,
			options: CreateSubscribeOptions(),
			cancellationToken: stoppingToken).ConfigureAwait(false);

		try
		{
			await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
		{
			LogStopped(topic, Constants.ResponseConsumerName);
		}
	}

	private async Task<MessageResult> HandleAsync(
		IServiceProvider services,
		MessageContext<IngredientQuoteResponseReceivedEvent> context)
	{
		try
		{
			var service = services.GetRequiredService<IIngredientSupply>();
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
			return MessageResult.Reject("The quote response is permanently invalid.");
		}
		catch (DbUpdateConcurrencyException exception)
		{
			LogConcurrencyRetry(exception, context.Envelope.Payload.QuoteId);
			return MessageResult.Retry("The quote response conflicted with a concurrent database update.");
		}
	}

	// Passed to Ruya's post-commit observer once the atomic Inbox commit succeeds. Keeping these
	// effects outside HandleAsync prevents execution-strategy retries and broker redelivery from
	// reporting a business transition that later rolls back.
	private Task ObserveCommittedAsync(
		IServiceProvider services,
		MessageContext<IngredientQuoteResponseReceivedEvent> context)
	{
		ArgumentNullException.ThrowIfNull(services);
		var message = context.Envelope.Payload;
		_responseReceived.Add(1,
			new KeyValuePair<string, object?>(Constants.Tags.Accepted, message.Accepted));
		if (!message.Accepted)
		{
			_failed.Add(1,
				new KeyValuePair<string, object?>(Constants.Tags.FailureStage, Constants.FailureStages.SupplierResponse));
		}
		LogQuoteResponseCommitted(message.QuoteId, message.Accepted);
		return Task.CompletedTask;
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

	[LoggerMessage(EventId = 5401, Level = LogLevel.Information, Message = "IngredientQuoteResponseReceivedEventSubscriber subscribed (topic='{Topic}', consumer='{Consumer}', provider='{ProviderName}').")]
	private partial void LogStarted(string topic, string consumer, string providerName);

	[LoggerMessage(EventId = 5402, Level = LogLevel.Warning, Message = "Rejecting permanently invalid IngredientQuoteResponseReceivedEvent for quote {QuoteId}.")]
	private partial void LogPermanentFailure(Exception exception, long quoteId);

	[LoggerMessage(EventId = 5403, Level = LogLevel.Information, Message = "IngredientQuoteResponseReceivedEventSubscriber stopped (topic='{Topic}', consumer='{Consumer}').")]
	private partial void LogStopped(string topic, string consumer);

	[LoggerMessage(EventId = 5404, Level = LogLevel.Warning, Message = "IngredientQuoteResponseReceivedEventSubscriber will retry quote {QuoteId} after a concurrent database update.")]
	private partial void LogConcurrencyRetry(Exception exception, long quoteId);

	[LoggerMessage(EventId = 5405, Level = LogLevel.Information, Message = "Quote {QuoteId} response committed. Accepted={Accepted}")]
	private partial void LogQuoteResponseCommitted(long quoteId, bool accepted);
}
