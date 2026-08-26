using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using BurcinCo.BurcinApp.Gateway.Integration.Tests.Fixtures;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Yarp.ReverseProxy.Configuration;

namespace BurcinCo.BurcinApp.Gateway.Integration.Tests;

[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public sealed class ReverseProxyProtectionTests
{
	private static readonly Uri ProxyUri = new("/proxy-protection", UriKind.Relative);

	[TestMethod]
	public void ReverseProxyRoutes_DefaultConfiguration_UseGatewayProtectionPolicies()
	{
		using var environment = ConfigureEnvironment();
		using var factory = new GatewayWebApplicationFactory();
		using var http = factory.CreateClient();

		var routes = factory.Services
			.GetRequiredService<IProxyConfigProvider>()
			.GetConfig()
			.Routes;

		Assert.IsNotEmpty(routes);
		foreach (var route in routes)
		{
			Assert.AreEqual(
				"gateway-proxy",
				route.RateLimiterPolicy,
				$"Reverse-proxy route '{route.RouteId}' must use the default proxy rate limit.");
			Assert.AreEqual(
				"gateway-proxy-ip-safelist",
				route.AuthorizationPolicy,
				$"Reverse-proxy route '{route.RouteId}' must expose the configurable CIDR safelist policy.");
		}
	}

	[TestMethod]
	public async Task GetAsync_ProxySafelistDeniesSocketPeer_DoesNotReachDestination()
	{
		await using var destination = ProxyDestinationRecorder.Start();
		using var environment = ConfigureEnvironment(
			hostDestination: destination.DestinationAddress,
			proxySafelistEnabled: true,
			proxyAllowedNetwork: "203.0.113.0/24");
		await using var factory = new GatewayWebApplicationFactory();
		using var http = factory.CreateClient();

		using var response = await http.GetAsync(ProxyUri);

		Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
		Assert.AreEqual(0, destination.CallCount, "A disallowed proxy client must not reach the destination.");
	}

	[TestMethod]
	public async Task GetAsync_ProxyRateLimitExceeded_DoesNotReachDestinationTwice()
	{
		await using var destination = ProxyDestinationRecorder.Start();
		using var environment = ConfigureEnvironment(
			hostDestination: destination.DestinationAddress,
			proxyTokenLimit: 1,
			proxyTokensPerPeriod: 1,
			proxyReplenishmentPeriod: "01:00:00");
		await using var factory = new GatewayWebApplicationFactory();
		using var http = factory.CreateClient();

		using var firstResponse = await http.GetAsync(ProxyUri);
		using var secondResponse = await http.GetAsync(ProxyUri);

		Assert.AreEqual(HttpStatusCode.OK, firstResponse.StatusCode);
		Assert.AreEqual(HttpStatusCode.TooManyRequests, secondResponse.StatusCode);
		Assert.AreEqual(1, destination.CallCount, "A rate-limited proxy request must not reach the destination.");
	}

	private static ProcessEnvironmentScope ConfigureEnvironment(
		Uri? hostDestination = null,
		bool proxySafelistEnabled = false,
		string? proxyAllowedNetwork = null,
		int proxyTokenLimit = 200,
		int proxyTokensPerPeriod = 50,
		string proxyReplenishmentPeriod = "00:00:05") =>
		ProcessEnvironmentScope.Apply(new Dictionary<string, string?>(StringComparer.Ordinal)
		{
			["DOTNET_ENVIRONMENT"] = "Development",
			["ASPNETCORE_ENVIRONMENT"] = "Development",
			["EnvironmentVariablesPrefix"] = "BURCINCO_",
			["FeatureManagement__Gateway.Webhook"] = bool.FalseString,
			["BURCINCO_FeatureManagement__Gateway.Webhook"] = bool.FalseString,
			["ReverseProxy__Clusters__host__HealthCheck__Active__Enabled"] = bool.FalseString,
			["BURCINCO_ReverseProxy__Clusters__host__HealthCheck__Active__Enabled"] = bool.FalseString,
			["ReverseProxy__Clusters__host__Destinations__host-1__Address"] = hostDestination?.AbsoluteUri,
			["BURCINCO_ReverseProxy__Clusters__host__Destinations__host-1__Address"] = hostDestination?.AbsoluteUri,
			["ReverseProxy__Clusters__portal__HealthCheck__Active__Enabled"] = bool.FalseString,
			["BURCINCO_ReverseProxy__Clusters__portal__HealthCheck__Active__Enabled"] = bool.FalseString,
			["Gateway__NetworkSecurity__IpSafelists__gateway-proxy-ip-safelist__Enabled"] = proxySafelistEnabled.ToString(CultureInfo.InvariantCulture),
			["BURCINCO_Gateway__NetworkSecurity__IpSafelists__gateway-proxy-ip-safelist__Enabled"] = proxySafelistEnabled.ToString(CultureInfo.InvariantCulture),
			["Gateway__NetworkSecurity__IpSafelists__gateway-proxy-ip-safelist__AllowedNetworks__0"] = proxyAllowedNetwork,
			["BURCINCO_Gateway__NetworkSecurity__IpSafelists__gateway-proxy-ip-safelist__AllowedNetworks__0"] = proxyAllowedNetwork,
			["Gateway__RateLimiting__Proxy__TokenLimit"] = proxyTokenLimit.ToString(CultureInfo.InvariantCulture),
			["BURCINCO_Gateway__RateLimiting__Proxy__TokenLimit"] = proxyTokenLimit.ToString(CultureInfo.InvariantCulture),
			["Gateway__RateLimiting__Proxy__TokensPerPeriod"] = proxyTokensPerPeriod.ToString(CultureInfo.InvariantCulture),
			["BURCINCO_Gateway__RateLimiting__Proxy__TokensPerPeriod"] = proxyTokensPerPeriod.ToString(CultureInfo.InvariantCulture),
			["Gateway__RateLimiting__Proxy__ReplenishmentPeriod"] = proxyReplenishmentPeriod,
			["BURCINCO_Gateway__RateLimiting__Proxy__ReplenishmentPeriod"] = proxyReplenishmentPeriod,
			["OTEL_EXPORTER_OTLP_ENDPOINT"] = string.Empty,
			["BURCINCO_OTEL_EXPORTER_OTLP_ENDPOINT"] = string.Empty,
			["LOGGING__CONSOLE__FORMATTERNAME"] = null,
		});
}
