using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using BurcinCo.BurcinApp.Gateway.Integration.Tests.Fixtures;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BurcinCo.BurcinApp.Gateway.Integration.Tests;

[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public sealed class HealthEndpointTests
{
	private static readonly Uri LiveHealthUri = new("/healthz/live", UriKind.Relative);
	private static readonly Uri CompatibilityHealthUri = new("/healthz", UriKind.Relative);

	[TestMethod]
	public async Task GetHealthzLive_Anonymous_ReturnsExactHealthy()
	{
		using var environment = ConfigureEnvironment();
		await using var factory = new GatewayWebApplicationFactory();
		using var http = factory.CreateClient();

		using var response = await http.GetAsync(LiveHealthUri);

		await AssertHealthyAsync(response);
		AssertAllowsAnonymous(factory, LiveHealthUri);
	}

	[TestMethod]
	public async Task GetHealthz_Anonymous_ReturnsExactHealthyWhenReady()
	{
		using var environment = ConfigureEnvironment();
		await using var factory = new GatewayWebApplicationFactory();
		using var http = factory.CreateClient();

		using var response = await GetWhenHealthyAsync(http, CompatibilityHealthUri);

		await AssertHealthyAsync(response);
		AssertAllowsAnonymous(factory, CompatibilityHealthUri);
	}

	private static ProcessEnvironmentScope ConfigureEnvironment() =>
		ProcessEnvironmentScope.Apply(new Dictionary<string, string?>(StringComparer.Ordinal)
		{
			["DOTNET_ENVIRONMENT"] = "Development",
			["ASPNETCORE_ENVIRONMENT"] = "Development",
			["EnvironmentVariablesPrefix"] = "BURCINCO_",
			// Health endpoints are process-local contracts. Keep optional edge capabilities and
			// active proxy probes disabled so these tests never depend on external resources.
			["FeatureManagement__Gateway.Webhook"] = bool.FalseString,
			["BURCINCO_FeatureManagement__Gateway.Webhook"] = bool.FalseString,
			["ReverseProxy__Clusters__host__HealthCheck__Active__Enabled"] = bool.FalseString,
			["BURCINCO_ReverseProxy__Clusters__host__HealthCheck__Active__Enabled"] = bool.FalseString,
			["ReverseProxy__Clusters__portal__HealthCheck__Active__Enabled"] = bool.FalseString,
			["BURCINCO_ReverseProxy__Clusters__portal__HealthCheck__Active__Enabled"] = bool.FalseString,
			["OTEL_EXPORTER_OTLP_ENDPOINT"] = string.Empty,
			["BURCINCO_OTEL_EXPORTER_OTLP_ENDPOINT"] = string.Empty,
			["LOGGING__CONSOLE__FORMATTERNAME"] = null,
		});

	private static async Task<HttpResponseMessage> GetWhenHealthyAsync(HttpClient http, Uri requestUri)
	{
		const int maximumAttempts = 50;
		var retryDelay = TimeSpan.FromMilliseconds(100);

		for (var attempt = 1; attempt < maximumAttempts; attempt++)
		{
			var response = await http.GetAsync(requestUri);
			if (response.StatusCode == HttpStatusCode.OK)
			{
				return response;
			}

			response.Dispose();
			await Task.Delay(retryDelay);
		}

		return await http.GetAsync(requestUri);
	}

	private static async Task AssertHealthyAsync(HttpResponseMessage response)
	{
		Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
		Assert.AreEqual("text/plain", response.Content.Headers.ContentType?.MediaType);
		Assert.AreEqual("Healthy", await response.Content.ReadAsStringAsync());
	}

	private static void AssertAllowsAnonymous(
		GatewayWebApplicationFactory factory,
		Uri requestUri)
	{
		var endpointDataSource = factory.Services.GetRequiredService<EndpointDataSource>();
		var endpoint = endpointDataSource.Endpoints
			.OfType<RouteEndpoint>()
			.Single(candidate => string.Equals(
				candidate.RoutePattern.RawText,
				requestUri.OriginalString,
				StringComparison.Ordinal));

		Assert.IsNotNull(
			endpoint.Metadata.GetMetadata<IAllowAnonymous>(),
			$"{requestUri} must remain explicitly anonymous.");
	}
}
