using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using BurcinCo.BurcinApp.Gateway.Webhook.Configuration;
using BurcinCo.BurcinApp.Gateway.Webhook.Contracts;
using BurcinCo.BurcinApp.Gateway.Webhook.Serialization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Polly.CircuitBreaker;
using Polly.Timeout;

using Ruya.Diagnostics.DistributedTracing;

namespace BurcinCo.BurcinApp.Gateway.Webhook;

internal sealed partial class WebhookService : IWebhook
{
	private readonly ILogger<WebhookService> _logger;
	private readonly IDistributedTracing _tracing;
	private readonly WebhookSettings _settings;
	private readonly IHttpClientFactory _httpClientFactory;
	private readonly TimeProvider _timeProvider;

	private readonly Counter<long> _received;
	private readonly Counter<long> _failures;
	private readonly Histogram<double> _duration;

	public WebhookService(
		ILogger<WebhookService> logger,
		IDistributedTracing tracing,
		IMeterFactory meterFactory,
		IOptions<WebhookSettings> settings,
		IHttpClientFactory httpClientFactory,
		TimeProvider timeProvider)
	{
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(tracing);
		ArgumentNullException.ThrowIfNull(meterFactory);
		ArgumentNullException.ThrowIfNull(settings);
		ArgumentNullException.ThrowIfNull(httpClientFactory);
		ArgumentNullException.ThrowIfNull(timeProvider);

		_logger = logger;
		_tracing = tracing;
		_settings = settings.Value;
		_httpClientFactory = httpClientFactory;
		_timeProvider = timeProvider;

		var meter = meterFactory.Create(Constants.Metrics.MeterName);
		_received = meter.CreateCounter<long>(Constants.Metrics.WebhookReceived, unit: "{webhook}");
		_failures = meter.CreateCounter<long>(Constants.Metrics.WebhookPublishFailures, unit: "{failure}");
		_duration = meter.CreateHistogram<double>(Constants.Metrics.WebhookPublishDuration, unit: "s");
	}

	public async Task<WebhookPublishResult> PublishAsync(
		string path,
		Stream body,
		long? contentLength,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		ArgumentNullException.ThrowIfNull(body);

		using var scope = _tracing.StartActivity(
			activityName: Constants.Activities.WebhookPublish,
			activityKind: ActivityKind.Producer,
			tags:
			[
				new KeyValuePair<string, object?>(Constants.Tags.InternalServiceName, Constants.ServiceName),
				new KeyValuePair<string, object?>(Constants.Tags.WebhookPath, path),
			]);

		var bodyRead = await ReadBodyAsync(body, contentLength, cancellationToken).ConfigureAwait(false);
		if (bodyRead.TooLarge)
		{
			_received.Add(1, new KeyValuePair<string, object?>(Constants.Tags.Outcome, "too_large"));
			scope.SetStatus(ActivityStatusCode.Error, "payload too large");
			return new WebhookPublishResult(WebhookPublishOutcome.PayloadTooLarge);
		}
		var bodyText = bodyRead.Body;

		var routingKey = $"webhooks.{path.Replace('/', '.')}";
		var exchange = routingKey;

		string payload;
		try
		{
			var payloadJson = string.IsNullOrWhiteSpace(bodyText) ? "{}" : bodyText;
			using var payloadDocument = JsonDocument.Parse(payloadJson);
			var envelope = new WebhookMessageEnvelope(
				Guid.NewGuid().ToString("D"),
				routingKey,
				_timeProvider.GetUtcNow(),
				Constants.ServiceName,
				payloadDocument.RootElement,
				true);
			var envelopeJson = JsonSerializer.Serialize(
				envelope,
				WebhookJsonSerializerContext.Default.WebhookMessageEnvelope);

			var publishRequest = new RabbitMqPublishRequest(
				new RabbitMqPublishProperties("application/json", 2),
				routingKey,
				envelopeJson,
				"string");
			payload = JsonSerializer.Serialize(
				publishRequest,
				WebhookJsonSerializerContext.Default.RabbitMqPublishRequest);
		}
		catch (JsonException exception)
		{
			_received.Add(1, new KeyValuePair<string, object?>(Constants.Tags.Outcome, "invalid_payload"));
			LogInvalidPayload(exception, path);
			scope.SetStatus(ActivityStatusCode.Error, "invalid JSON payload");
			return new WebhookPublishResult(
				WebhookPublishOutcome.InvalidPayload,
				"The request body must contain valid JSON.");
		}

		var client = _httpClientFactory.CreateClient(Constants.HttpClients.RabbitMqManagement);
		var stopwatch = Stopwatch.StartNew();
		try
		{
			using var content = new StringContent(payload, Encoding.UTF8, "application/json");
			using var response = await client.PostAsync(
				$"/api/exchanges/{_settings.VHost}/{Uri.EscapeDataString(exchange)}/publish",
				content,
				cancellationToken).ConfigureAwait(false);

			if (!response.IsSuccessStatusCode)
			{
				var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
				_received.Add(1, new KeyValuePair<string, object?>(Constants.Tags.Outcome, "broker_error"));
				_failures.Add(1, new KeyValuePair<string, object?>(Constants.Tags.Reason, "broker_non_success"));
				LogBrokerNonSuccess(path, response.StatusCode, error);
				scope.SetStatus(ActivityStatusCode.Error, $"broker {(int)response.StatusCode}");
				return new WebhookPublishResult(WebhookPublishOutcome.BrokerError, error);
			}

			RabbitMqPublishResponse? publishResponse;
			try
			{
				publishResponse = await response.Content.ReadFromJsonAsync(
					WebhookJsonSerializerContext.Default.RabbitMqPublishResponse,
					cancellationToken).ConfigureAwait(false);
			}
			catch (JsonException exception)
			{
				_received.Add(1, new KeyValuePair<string, object?>(Constants.Tags.Outcome, "broker_error"));
				_failures.Add(1, new KeyValuePair<string, object?>(Constants.Tags.Reason, "broker_invalid_response"));
				LogBrokerInvalidResponse(exception, path);
				scope.SetStatus(ActivityStatusCode.Error, "broker returned invalid publish response");
				return new WebhookPublishResult(
					WebhookPublishOutcome.BrokerError,
					"The broker returned an invalid publish response.");
			}

			if (publishResponse is not { Routed: true })
			{
				_received.Add(1, new KeyValuePair<string, object?>(Constants.Tags.Outcome, "broker_error"));
				_failures.Add(1, new KeyValuePair<string, object?>(Constants.Tags.Reason, "broker_unrouted"));
				LogBrokerUnrouted(path, routingKey);
				scope.SetStatus(ActivityStatusCode.Error, "broker did not route message");
				return new WebhookPublishResult(
					WebhookPublishOutcome.BrokerError,
					"The broker did not route the message.");
			}

			_received.Add(1, new KeyValuePair<string, object?>(Constants.Tags.Outcome, "accepted"));
			scope.SetStatus(ActivityStatusCode.Ok);
			return new WebhookPublishResult(WebhookPublishOutcome.Accepted);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex) when (
			ex is HttpRequestException or TaskCanceledException or TimeoutRejectedException or BrokenCircuitException)
		{
			_received.Add(1, new KeyValuePair<string, object?>(Constants.Tags.Outcome, "broker_error"));
			_failures.Add(1, new KeyValuePair<string, object?>(Constants.Tags.Reason, "transport"));
			LogTransportFailure(ex, path);
			scope.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
			return new WebhookPublishResult(WebhookPublishOutcome.BrokerError, ex.Message);
		}
		finally
		{
			stopwatch.Stop();
			_duration.Record(stopwatch.Elapsed.TotalSeconds);
		}
	}

	private async Task<(string Body, bool TooLarge)> ReadBodyAsync(
		Stream body,
		long? contentLength,
		CancellationToken cancellationToken)
	{
		if (contentLength is > 0 && contentLength > _settings.MaxBodyBytes)
		{
			return (string.Empty, true);
		}

		var initialCapacity = checked((int)Math.Min(_settings.MaxBodyBytes, 64L * 1024));
		using var payload = new MemoryStream(initialCapacity);
		var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
		try
		{
			while (true)
			{
				var bytesRemaining = _settings.MaxBodyBytes - payload.Length;
				var readLength = checked((int)Math.Min(buffer.Length, bytesRemaining + 1));
				var bytesRead = await body.ReadAsync(
					buffer.AsMemory(0, readLength),
					cancellationToken).ConfigureAwait(false);
				if (bytesRead == 0)
				{
					break;
				}
				if (payload.Length + bytesRead > _settings.MaxBodyBytes)
				{
					return (string.Empty, true);
				}

				await payload.WriteAsync(
					buffer.AsMemory(0, bytesRead),
					cancellationToken).ConfigureAwait(false);
			}

			return (
				Encoding.UTF8.GetString(payload.GetBuffer(), 0, checked((int)payload.Length)),
				false);
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
		}
	}

	[LoggerMessage(
		EventId = 2001,
		Level = LogLevel.Error,
		Message = "Webhook publish failed for {Path}: {StatusCode} {Error}")]
	private partial void LogBrokerNonSuccess(string path, HttpStatusCode statusCode, string error);

	[LoggerMessage(
		EventId = 2002,
		Level = LogLevel.Error,
		Message = "Webhook publish transport failure for path {Path}")]
	private partial void LogTransportFailure(Exception exception, string path);

	[LoggerMessage(
		EventId = 2003,
		Level = LogLevel.Warning,
		Message = "Webhook payload for path {Path} is not valid JSON.")]
	private partial void LogInvalidPayload(Exception exception, string path);

	[LoggerMessage(
		EventId = 2004,
		Level = LogLevel.Error,
		Message = "RabbitMQ returned an invalid publish response for webhook path {Path}.")]
	private partial void LogBrokerInvalidResponse(Exception exception, string path);

	[LoggerMessage(
		EventId = 2005,
		Level = LogLevel.Error,
		Message = "RabbitMQ did not route webhook path {Path} to routing key {RoutingKey}.")]
	private partial void LogBrokerUnrouted(string path, string routingKey);
}
