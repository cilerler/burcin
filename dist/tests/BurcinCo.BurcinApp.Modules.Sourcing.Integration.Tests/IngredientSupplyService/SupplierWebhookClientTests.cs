using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Models;
using BurcinCo.BurcinApp.Modules.Sourcing.Integration.Tests.Fixtures;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Abstractions.Events;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Clients;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Exceptions;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ruya.Diagnostics.DistributedTracing;
using IngredientSupplyConstants = BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Constants;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Integration.Tests.IngredientSupplyService;

/// <summary>Focused classification tests for the supplier boundary used by the request subscriber.</summary>
[TestClass]
public sealed class SupplierWebhookClientTests
{
	[TestMethod]
	public async Task AddIngredientSupply_DoesNotRetryTransientSupplierPost()
	{
		var attempt = 0;
		using var handler = new StubSupplierHandler(_ =>
			Interlocked.Increment(ref attempt) == 1
				? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
				: new HttpResponseMessage(HttpStatusCode.OK));
		using var provider = CreateResilientServiceProvider(handler, TimeSpan.FromSeconds(5));
		using var scope = provider.CreateScope();
		var sut = scope.ServiceProvider.GetRequiredService<SupplierWebhookClient>();

		await AssertThrowsAsync<TransientSupplierException>(() =>
			sut.PostQuoteRequestAsync(CreateEvent(), CancellationToken.None));

		Assert.AreEqual(1, handler.ReceivedRequests.Count);
		Assert.AreEqual(1, handler.ReceivedRequests
			.Select(request => request.Headers.GetValues("Idempotency-Key").Single())
			.Distinct(StringComparer.Ordinal)
			.Count());
	}

	[TestMethod]
	public async Task AddIngredientSupply_PipelineTimeout_IsClassifiedAsTransient()
	{
		using var handler = new CancellationSupplierHandler();
		using var provider = CreateResilientServiceProvider(handler, TimeSpan.FromMilliseconds(25));
		using var scope = provider.CreateScope();
		var sut = scope.ServiceProvider.GetRequiredService<SupplierWebhookClient>();

		await AssertThrowsAsync<TransientSupplierException>(() =>
			sut.PostQuoteRequestAsync(CreateEvent(), CancellationToken.None));
	}

	[DataTestMethod]
	[DataRow(408)]
	[DataRow(429)]
	[DataRow(500)]
	[DataRow(503)]
	public async Task PostQuoteRequestAsync_TransientHttpStatus_ThrowsConcreteTransientException(int statusCode)
	{
		using var handler = new StubSupplierHandler((HttpStatusCode)statusCode);
		var sut = CreateClient(handler);

		await AssertThrowsAsync<TransientSupplierException>(() =>
			sut.PostQuoteRequestAsync(CreateEvent(), CancellationToken.None));
	}

	[DataTestMethod]
	[DataRow(300)]
	[DataRow(400)]
	[DataRow(401)]
	[DataRow(404)]
	public async Task PostQuoteRequestAsync_PermanentHttpStatus_ThrowsConcreteInvalidMessageException(int statusCode)
	{
		using var handler = new StubSupplierHandler((HttpStatusCode)statusCode);
		var sut = CreateClient(handler);

		await AssertThrowsAsync<InvalidIngredientQuoteMessageException>(() =>
			sut.PostQuoteRequestAsync(CreateEvent(), CancellationToken.None));
	}

	[TestMethod]
	public async Task PostQuoteRequestAsync_TransportFailure_ThrowsConcreteTransientException()
	{
		using var handler = new StubSupplierHandler(_ => throw new HttpRequestException("Simulated transport failure."));
		var sut = CreateClient(handler);

		await AssertThrowsAsync<TransientSupplierException>(() =>
			sut.PostQuoteRequestAsync(CreateEvent(), CancellationToken.None));
	}

	[TestMethod]
	public async Task PostQuoteRequestAsync_ClientTimeout_ThrowsConcreteTransientException()
	{
		using var handler = new TimeoutSupplierHandler();
		var sut = CreateClient(handler);

		await AssertThrowsAsync<TransientSupplierException>(() =>
			sut.PostQuoteRequestAsync(CreateEvent(), CancellationToken.None));
	}

	[TestMethod]
	public async Task PostQuoteRequestAsync_DeliveryCancellation_PropagatesCancellation()
	{
		using var handler = new CancellationSupplierHandler();
		var sut = CreateClient(handler);
		using var cancellation = new CancellationTokenSource();
		await cancellation.CancelAsync();

		await AssertThrowsAsync<OperationCanceledException>(() =>
			sut.PostQuoteRequestAsync(CreateEvent(), cancellation.Token));
	}

	private static SupplierWebhookClient CreateClient(HttpMessageHandler handler)
	{
		var factory = new TestHttpClientFactory(handler);
		var options = Options.Create(new SupplierWebhookClientSettings
		{
			Suppliers = new Dictionary<string, SupplierEndpoint>(StringComparer.Ordinal)
			{
				["test-supplier"] = new SupplierEndpoint { Url = "http://supplier.test/quote" },
			},
			HttpTimeout = TimeSpan.FromSeconds(1),
		});
		return new SupplierWebhookClient(
			factory,
			options,
			NullLogger<SupplierWebhookClient>.Instance);
	}

	private static ServiceProvider CreateResilientServiceProvider(
		HttpMessageHandler handler,
		TimeSpan httpTimeout)
	{
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
		{
			["DistributedTracing:CacheSlidingExpiration"] = "1.00:00:00",
			["DistributedTracing:CacheAbsoluteExpiration"] = "1.00:00:00",
			["Modules:Sourcing:Procurement:IngredientSupply:Clients:HttpTimeout"]
				= httpTimeout.ToString("c", System.Globalization.CultureInfo.InvariantCulture),
			["Modules:Sourcing:Procurement:IngredientSupply:Clients:Suppliers:test-supplier:Url"]
				= "http://supplier.test/quote",
		}).Build();
		var services = new ServiceCollection();
		services.AddSingleton<IConfiguration>(configuration);
		services.AddLogging();
		services.AddMetrics();
		services.AddDistributedMemoryCache();
		services.AddDistributedTracingService();
		services.AddIngredientSupply();
		services.AddHttpClient(IngredientSupplyConstants.HttpClients.SupplierWebhook)
			.ConfigurePrimaryHttpMessageHandler(_ => handler);

		return services.BuildServiceProvider();
	}

	private static IngredientQuoteRequestedEvent CreateEvent() => new(
		QuoteId: 42,
		RecipeId: null,
		SupplierKey: "test-supplier",
		Ingredients: Array.Empty<IngredientLine>(),
		RequestedAt: DateTimeOffset.UtcNow);

	private static async Task AssertThrowsAsync<TException>(Func<Task> action)
		where TException : Exception
	{
		try
		{
			await action().ConfigureAwait(false);
			Assert.Fail($"Expected {typeof(TException).Name}.");
		}
		catch (TException)
		{
			// Expected exact classification (derived cancellation types are valid for OperationCanceledException).
		}
	}

	private sealed class TestHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
	{
		public HttpClient CreateClient(string _) => new(handler, disposeHandler: false);
	}

	private sealed class CancellationSupplierHandler : HttpMessageHandler
	{
		protected override async Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage _,
			CancellationToken cancellationToken)
		{
			await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
			throw new InvalidOperationException("Cancellation was not observed.");
		}
	}

	private sealed class TimeoutSupplierHandler : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage _,
			CancellationToken cancellationToken) =>
			Task.FromException<HttpResponseMessage>(new TaskCanceledException("Simulated client timeout."));
	}
}
