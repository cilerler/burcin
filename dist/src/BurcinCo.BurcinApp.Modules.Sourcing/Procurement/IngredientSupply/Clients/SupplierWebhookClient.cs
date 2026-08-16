using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Abstractions.Events;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Clients;

/// <summary>
/// Outbound HTTP client wrapping the call to an external supplier's webhook endpoint.
/// One client instance is created per outbound request; the supplier URL + secret come from
/// <see cref="SupplierWebhookClientSettings"/> keyed by <c>SupplierKey</c>. Convention-located
/// here per <c>Clients/</c> = "wraps external HTTP API".
/// </summary>
internal sealed partial class SupplierWebhookClient
{
	private readonly IHttpClientFactory _httpClientFactory;
	private readonly SupplierWebhookClientSettings _settings;
	private readonly ILogger<SupplierWebhookClient> _logger;

	public SupplierWebhookClient(
		IHttpClientFactory httpClientFactory,
		IOptions<SupplierWebhookClientSettings> settings,
		ILogger<SupplierWebhookClient> logger)
	{
		ArgumentNullException.ThrowIfNull(httpClientFactory);
		ArgumentNullException.ThrowIfNull(settings);
		ArgumentNullException.ThrowIfNull(logger);
		_httpClientFactory = httpClientFactory;
		_settings = settings.Value;
		_logger = logger;
	}

	/// <summary>
	/// POST the quote-requested event to the external supplier's configured webhook URL.
	/// Sends a stable <c>Idempotency-Key</c> derived from the quote id so the supplier can
	/// collapse repeated requests. Broker redelivery and transaction retries cannot roll back
	/// an HTTP side effect, so the receiving supplier must enforce this key idempotently.
	/// Returns on 2xx. Transport failures, client-side timeouts, 408, 429, and 5xx responses are
	/// surfaced as <see cref="TransientSupplierException"/>; missing supplier configuration and
	/// other non-success responses are permanent <see cref="InvalidIngredientQuoteMessageException"/> failures.
	/// </summary>
	public async Task PostQuoteRequestAsync(IngredientQuoteRequestedEvent ev, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(ev);

		if (!_settings.Suppliers.TryGetValue(ev.SupplierKey, out var endpoint) || string.IsNullOrWhiteSpace(endpoint.Url))
		{
			LogSupplierNotConfigured(ev.SupplierKey, ev.QuoteId);
			throw new InvalidIngredientQuoteMessageException(
				$"Supplier '{ev.SupplierKey}' is not configured for quote {ev.QuoteId}.");
		}
		if (!Uri.TryCreate(endpoint.Url, UriKind.Absolute, out var endpointUri) ||
			(!string.Equals(endpointUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
			 !string.Equals(endpointUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
		{
			LogSupplierEndpointInvalid(ev.SupplierKey, ev.QuoteId, endpoint.Url);
			throw new InvalidIngredientQuoteMessageException(
				$"Supplier '{ev.SupplierKey}' has an invalid HTTP endpoint for quote {ev.QuoteId}.");
		}

		using var client = _httpClientFactory.CreateClient(Constants.HttpClients.SupplierWebhook);
		using var request = new HttpRequestMessage(HttpMethod.Post, endpointUri)
		{
			Content = JsonContent.Create(
				ev,
				SupplierWebhookJsonSerializerContext.Default.IngredientQuoteRequestedEvent),
		};
		// HTTP cannot enlist in the subscriber's database transaction. A stable key makes a repeated
		// delivery or execution attempt recognizable to the supplier without changing request semantics.
		request.Headers.Add("Idempotency-Key", ev.QuoteId.ToString(CultureInfo.InvariantCulture));
		if (!string.IsNullOrWhiteSpace(endpoint.Secret))
		{
			request.Headers.Add("X-Webhook-Secret", endpoint.Secret);
		}

		try
		{
			using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
			if (response.IsSuccessStatusCode)
			{
				LogSupplierSent(ev.SupplierKey, ev.QuoteId);
				return;
			}

			var statusCode = (int)response.StatusCode;
			LogSupplierNonSuccess(ev.SupplierKey, ev.QuoteId, statusCode);
			if (statusCode is 408 or 429 || statusCode >= 500)
			{
				throw new TransientSupplierException(
					$"Supplier '{ev.SupplierKey}' returned transient HTTP status {statusCode} for quote {ev.QuoteId}.");
			}

			throw new InvalidIngredientQuoteMessageException(
				$"Supplier '{ev.SupplierKey}' permanently rejected quote {ev.QuoteId} with HTTP status {statusCode}.");
		}
		catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
		{
			LogSupplierTimeout(ex, ev.SupplierKey, ev.QuoteId);
			throw new TransientSupplierException(
				$"Supplier '{ev.SupplierKey}' timed out while processing quote {ev.QuoteId}.", ex);
		}
		catch (TimeoutRejectedException ex)
		{
			LogSupplierTimeout(ex, ev.SupplierKey, ev.QuoteId);
			throw new TransientSupplierException(
				$"Supplier '{ev.SupplierKey}' timed out while processing quote {ev.QuoteId}.", ex);
		}
		catch (BrokenCircuitException ex)
		{
			LogSupplierTransport(ex, ev.SupplierKey, ev.QuoteId);
			throw new TransientSupplierException(
				$"Supplier '{ev.SupplierKey}' is temporarily unavailable for quote {ev.QuoteId}.", ex);
		}
		catch (HttpRequestException ex)
		{
			LogSupplierTransport(ex, ev.SupplierKey, ev.QuoteId);
			throw new TransientSupplierException(
				$"Supplier '{ev.SupplierKey}' could not be reached for quote {ev.QuoteId}.", ex);
		}
	}

	[LoggerMessage(EventId = 5101, Level = LogLevel.Warning, Message = "Supplier '{SupplierKey}' not configured (Quote {QuoteId}); delivery will be permanently rejected.")]
	private partial void LogSupplierNotConfigured(string supplierKey, long quoteId);

	[LoggerMessage(EventId = 5102, Level = LogLevel.Information, Message = "Quote {QuoteId} sent to supplier '{SupplierKey}'.")]
	private partial void LogSupplierSent(string supplierKey, long quoteId);

	[LoggerMessage(EventId = 5103, Level = LogLevel.Warning, Message = "Supplier '{SupplierKey}' returned {StatusCode} for Quote {QuoteId}.")]
	private partial void LogSupplierNonSuccess(string supplierKey, long quoteId, int statusCode);

	[LoggerMessage(EventId = 5104, Level = LogLevel.Error, Message = "Supplier '{SupplierKey}' transport failure for Quote {QuoteId}.")]
	private partial void LogSupplierTransport(System.Exception exception, string supplierKey, long quoteId);

	[LoggerMessage(EventId = 5105, Level = LogLevel.Warning, Message = "Supplier '{SupplierKey}' timed out for Quote {QuoteId}.")]
	private partial void LogSupplierTimeout(System.Exception exception, string supplierKey, long quoteId);

	[LoggerMessage(EventId = 5106, Level = LogLevel.Warning, Message = "Supplier '{SupplierKey}' has invalid endpoint '{Endpoint}' for Quote {QuoteId}; delivery will be permanently rejected.")]
	private partial void LogSupplierEndpointInvalid(string supplierKey, long quoteId, string endpoint);
}
