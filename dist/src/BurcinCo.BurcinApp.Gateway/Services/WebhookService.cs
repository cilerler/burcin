using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using BurcinCo.BurcinApp.Gateway.Configuration;
using BurcinCo.BurcinApp.Gateway.Contracts;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Ruya.Diagnostics.DistributedTracing;

namespace BurcinCo.BurcinApp.Gateway.Services;

internal sealed partial class WebhookService : IWebhookService
{
	private readonly ILogger<WebhookService> _logger;
	private readonly IDistributedTracing _tracing;
	private readonly WebhookServiceSettings _settings;
	private readonly IHttpClientFactory _httpClientFactory;

	private readonly Counter<long> _received;
	private readonly Counter<long> _failures;
	private readonly Histogram<double> _duration;

	public WebhookService(
		ILogger<WebhookService> logger,
		IDistributedTracing tracing,
		IMeterFactory meterFactory,
		IOptions<WebhookServiceSettings> settings,
		IHttpClientFactory httpClientFactory)
	{
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(tracing);
		ArgumentNullException.ThrowIfNull(meterFactory);
		ArgumentNullException.ThrowIfNull(settings);
		ArgumentNullException.ThrowIfNull(httpClientFactory);

		_logger = logger;
		_tracing = tracing;
		_settings = settings.Value;
		_httpClientFactory = httpClientFactory;

		var meter = meterFactory.Create(Constants.Metrics.MeterName);
		_received = meter.CreateCounter<long>(Constants.Metrics.WebhookReceived, unit: "{webhook}");
		_failures = meter.CreateCounter<long>(Constants.Metrics.WebhookPublishFailures, unit: "{failure}");
		_duration = meter.CreateHistogram<double>(Constants.Metrics.WebhookPublishDuration, unit: "ms");
	}

	public async Task<WebhookPublishResult> PublishAsync(string path, string body, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(path);
		body ??= string.Empty;

		using var scope = _tracing.StartActivity(
			activityName: Constants.Activities.WebhookPublish,
			activityKind: ActivityKind.Producer,
			tags: new[] { new KeyValuePair<string, object?>(Constants.Tags.WebhookPath, path) });

		if (Encoding.UTF8.GetByteCount(body) > _settings.MaxBodyBytes)
		{
			_received.Add(1,
				new KeyValuePair<string, object?>(Constants.Tags.WebhookPath, path),
				new KeyValuePair<string, object?>(Constants.Tags.Outcome, "too_large"));
			scope.SetStatus(ActivityStatusCode.Error, "payload too large");
			return new WebhookPublishResult(WebhookPublishOutcome.PayloadTooLarge);
		}

		var routingKey = $"webhooks.{path.Replace('/', '.')}";
		var payload = JsonSerializer.Serialize(new
		{
			properties = new { },
			routing_key = routingKey,
			payload = body,
			payload_encoding = "string",
		});

		var client = _httpClientFactory.CreateClient(Constants.HttpClients.RabbitMqManagement);

		var stopwatch = Stopwatch.StartNew();
		try
		{
			using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			timeoutCts.CancelAfter(_settings.PublishTimeout);

			using var content = new StringContent(payload, Encoding.UTF8, "application/json");
			var response = await client.PostAsync(
				$"/api/exchanges/{_settings.VHost}/{_settings.Exchange}/publish",
				content,
				timeoutCts.Token).ConfigureAwait(false);

			stopwatch.Stop();
			_duration.Record(stopwatch.Elapsed.TotalMilliseconds,
				new KeyValuePair<string, object?>(Constants.Tags.WebhookPath, path));

			if (!response.IsSuccessStatusCode)
			{
				var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
				_failures.Add(1, new KeyValuePair<string, object?>(Constants.Tags.Reason, "broker_non_success"));
				LogBrokerNonSuccess(path, response.StatusCode, error);
				scope.SetStatus(ActivityStatusCode.Error, $"broker {(int)response.StatusCode}");
				return new WebhookPublishResult(WebhookPublishOutcome.BrokerError, error);
			}

			_received.Add(1,
				new KeyValuePair<string, object?>(Constants.Tags.WebhookPath, path),
				new KeyValuePair<string, object?>(Constants.Tags.Outcome, "accepted"));
			scope.SetStatus(ActivityStatusCode.Ok);
			return new WebhookPublishResult(WebhookPublishOutcome.Accepted);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			// Caller cancelled — propagate, don't translate to a broker error.
			throw;
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
		{
			// HttpRequestException = network/transport failure.
			// TaskCanceledException (without the caller's cancellationToken being set) = publish timeout via CancelAfter.
			stopwatch.Stop();
			_failures.Add(1, new KeyValuePair<string, object?>(Constants.Tags.Reason, "transport"));
			LogTransportFailure(ex, path);
			scope.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
			return new WebhookPublishResult(WebhookPublishOutcome.BrokerError, ex.Message);
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
}
