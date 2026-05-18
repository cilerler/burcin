using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
	/// Returns true on 2xx, false otherwise.
	/// </summary>
	public async Task<bool> PostQuoteRequestAsync(IngredientQuoteRequestedEvent ev, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(ev);

		if (!_settings.Suppliers.TryGetValue(ev.SupplierKey, out var endpoint) || string.IsNullOrWhiteSpace(endpoint.Url))
		{
			LogSupplierNotConfigured(ev.SupplierKey, ev.QuoteId);
			return false;
		}

		using var client = _httpClientFactory.CreateClient(nameof(SupplierWebhookClient));
		client.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
		using var request = new HttpRequestMessage(HttpMethod.Post, endpoint.Url)
		{
			Content = JsonContent.Create(ev),
		};
		if (!string.IsNullOrWhiteSpace(endpoint.Secret))
		{
			request.Headers.Add("X-Webhook-Secret", endpoint.Secret);
		}

		try
		{
			using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
			if (!response.IsSuccessStatusCode)
			{
				LogSupplierNonSuccess(ev.SupplierKey, ev.QuoteId, (int)response.StatusCode);
				return false;
			}
			LogSupplierSent(ev.SupplierKey, ev.QuoteId);
			return true;
		}
		catch (HttpRequestException ex)
		{
			LogSupplierTransport(ex, ev.SupplierKey, ev.QuoteId);
			return false;
		}
	}

	[LoggerMessage(EventId = 5101, Level = LogLevel.Warning, Message = "Supplier '{SupplierKey}' not configured (Quote {QuoteId}); event dropped.")]
	private partial void LogSupplierNotConfigured(string supplierKey, long quoteId);

	[LoggerMessage(EventId = 5102, Level = LogLevel.Information, Message = "Quote {QuoteId} sent to supplier '{SupplierKey}'.")]
	private partial void LogSupplierSent(string supplierKey, long quoteId);

	[LoggerMessage(EventId = 5103, Level = LogLevel.Warning, Message = "Supplier '{SupplierKey}' returned {StatusCode} for Quote {QuoteId}.")]
	private partial void LogSupplierNonSuccess(string supplierKey, long quoteId, int statusCode);

	[LoggerMessage(EventId = 5104, Level = LogLevel.Error, Message = "Supplier '{SupplierKey}' transport failure for Quote {QuoteId}.")]
	private partial void LogSupplierTransport(System.Exception exception, string supplierKey, long quoteId);
}
