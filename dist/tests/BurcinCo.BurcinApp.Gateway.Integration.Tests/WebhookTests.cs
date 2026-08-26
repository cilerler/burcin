using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using BurcinCo.BurcinApp.Gateway.Integration.Tests.Fixtures;

using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BurcinCo.BurcinApp.Gateway.Integration.Tests;

[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public sealed class WebhookTests
{
	private const string WebhookRoutePattern = "/webhooks/{**path}";
	private static readonly Uri WebhookUri = new("/webhooks/sourcing/quote-response", UriKind.Relative);

	[TestMethod]
	public void CreateClient_WebhookDisabled_DoesNotMapWebhookRoute()
	{
		using var environment = ConfigureEnvironment(
			webhookEnabled: false,
			managementEndpoint: "not-an-absolute-uri",
			authRequired: true,
			authSecret: null);
		using var factory = new GatewayWebApplicationFactory();
		using var http = factory.CreateClient();

		var endpointDataSource = factory.Services.GetRequiredService<EndpointDataSource>();
		var webhookRouteIsMapped = endpointDataSource.Endpoints
			.OfType<RouteEndpoint>()
			.Any(endpoint => string.Equals(
				endpoint.RoutePattern.RawText,
				WebhookRoutePattern,
				StringComparison.Ordinal));

		Assert.IsFalse(
			webhookRouteIsMapped,
			"A disabled Gateway edge capability must publish no Webhook route.");
	}

	[TestMethod]
	public async Task PostAsync_ValidJson_ReturnsAcceptedAndPublishesExactlyOnce()
	{
		await using var broker = BrokerCallRecorder.Start();
		using var environment = ConfigureEnvironment(
			webhookEnabled: true,
			managementEndpoint: broker.ManagementEndpoint.AbsoluteUri,
			authRequired: false,
			authSecret: null);
		using var factory = new GatewayWebApplicationFactory();
		using var http = factory.CreateClient();

		using var content = new StringContent("{\"supplier\":\"alpha\"}", Encoding.UTF8, "application/json");
		using var response = await http.PostAsync(WebhookUri, content);

		Assert.AreEqual(HttpStatusCode.Accepted, response.StatusCode);
		Assert.AreEqual(1, broker.CallCount, "A valid Webhook request must publish exactly once.");
		StringAssert.Contains(
			broker.LastRequest,
			"POST /api/exchanges/%2F/webhooks.sourcing.quote-response/publish",
			StringComparison.Ordinal);
		StringAssert.Contains(broker.LastRequest, "webhooks.sourcing.quote-response", StringComparison.Ordinal);
		StringAssert.Contains(broker.LastRequest, "supplier", StringComparison.Ordinal);
	}

	[TestMethod]
	public async Task PostAsync_OversizedDeclaredAndChunkedBodies_ReturnsPayloadTooLargeWithoutPublishing()
	{
		const int maxBodyBytes = 16;
		var oversizedPayload = new string('x', maxBodyBytes * 2);
		await using var broker = BrokerCallRecorder.Start();
		using var environment = ConfigureEnvironment(
			webhookEnabled: true,
			managementEndpoint: broker.ManagementEndpoint.AbsoluteUri,
			authRequired: false,
			authSecret: null,
			maxBodyBytes: maxBodyBytes);
		using var factory = new GatewayWebApplicationFactory();
		using var http = factory.CreateClient();

		using var declaredContent = new StringContent(oversizedPayload, Encoding.UTF8, "application/json");
		Assert.IsTrue(
			declaredContent.Headers.ContentLength > maxBodyBytes,
			"The declared-length case must advertise an oversized Content-Length.");
		using var declaredRequest = CreateWebhookRequest(declaredContent);
		using var declaredResponse = await http.SendAsync(declaredRequest);

		Assert.AreEqual(HttpStatusCode.RequestEntityTooLarge, declaredResponse.StatusCode);
		Assert.AreEqual(0, broker.CallCount, "Declared-length rejection must not call the broker management API.");

		using var chunkedContent = new NoContentLengthHttpContent(oversizedPayload);
		Assert.IsNull(
			chunkedContent.Headers.ContentLength,
			"The streaming case must not expose a Content-Length.");
		using var chunkedRequest = CreateWebhookRequest(chunkedContent);
		chunkedRequest.Headers.TransferEncodingChunked = true;
		using var chunkedResponse = await http.SendAsync(chunkedRequest);

		Assert.AreEqual(HttpStatusCode.RequestEntityTooLarge, chunkedResponse.StatusCode);
		Assert.AreEqual(0, broker.CallCount, "Chunked rejection must not call the broker management API.");
	}

	[TestMethod]
	public async Task PostAsync_WebhookRateLimitExceeded_ReturnsProblemWithoutSecondPublish()
	{
		await using var broker = BrokerCallRecorder.Start();
		using var environment = ConfigureEnvironment(
			webhookEnabled: true,
			managementEndpoint: broker.ManagementEndpoint.AbsoluteUri,
			authRequired: false,
			authSecret: null,
			webhookTokenLimit: 1,
			webhookTokensPerPeriod: 1,
			webhookReplenishmentPeriod: "01:00:00");
		using var factory = new GatewayWebApplicationFactory();
		using var http = factory.CreateClient();

		using var firstContent = new StringContent("{}", Encoding.UTF8, "application/json");
		using var firstResponse = await http.PostAsync(WebhookUri, firstContent);
		using var secondContent = new StringContent("{}", Encoding.UTF8, "application/json");
		using var secondResponse = await http.PostAsync(WebhookUri, secondContent);

		Assert.AreEqual(HttpStatusCode.Accepted, firstResponse.StatusCode);
		Assert.AreEqual(HttpStatusCode.TooManyRequests, secondResponse.StatusCode);
		Assert.AreEqual("application/problem+json", secondResponse.Content.Headers.ContentType?.MediaType);
		Assert.IsNotNull(secondResponse.Headers.RetryAfter);
		StringAssert.Contains(
			await secondResponse.Content.ReadAsStringAsync(),
			"Too many requests",
			StringComparison.Ordinal);
		Assert.AreEqual(1, broker.CallCount, "A rejected request must not reach RabbitMQ.");
	}

	[TestMethod]
	public async Task PostAsync_WebhookIpSafelistDeniesSocketPeer_ReturnsForbiddenWithoutPublishing()
	{
		await using var broker = BrokerCallRecorder.Start();
		using var environment = ConfigureEnvironment(
			webhookEnabled: true,
			managementEndpoint: broker.ManagementEndpoint.AbsoluteUri,
			authRequired: false,
			authSecret: null,
			webhookIpSafelistEnabled: true,
			webhookAllowedNetworks: ["203.0.113.0/24"]);
		using var factory = new GatewayWebApplicationFactory();
		using var http = factory.CreateClient();

		using var content = new StringContent("{}", Encoding.UTF8, "application/json");
		using var response = await http.PostAsync(WebhookUri, content);

		Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
		Assert.AreEqual(string.Empty, await response.Content.ReadAsStringAsync());
		Assert.AreEqual(0, broker.CallCount, "A disallowed client must not reach RabbitMQ.");
	}

	[TestMethod]
	public async Task PostAsync_SpoofedForwardedAddressIsIgnored_ReturnsForbiddenWithoutPublishing()
	{
		await using var broker = BrokerCallRecorder.Start();
		using var environment = ConfigureEnvironment(
			webhookEnabled: true,
			managementEndpoint: broker.ManagementEndpoint.AbsoluteUri,
			authRequired: false,
			authSecret: null,
			webhookIpSafelistEnabled: true,
			webhookAllowedNetworks: ["203.0.113.0/24"]);
		using var factory = new GatewayWebApplicationFactory();
		using var http = factory.CreateClient();
		using var request = CreateWebhookRequest(
			new StringContent("{}", Encoding.UTF8, "application/json"));
		request.Headers.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.25");

		using var response = await http.SendAsync(request);

		Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
		Assert.AreEqual(0, broker.CallCount, "An untrusted forwarded address must not bypass the safelist.");
	}

	[TestMethod]
	public async Task PostAsync_ForwardedAddressFromTrustedProxy_UsesOriginalClientAddress()
	{
		await using var broker = BrokerCallRecorder.Start();
		using var environment = ConfigureEnvironment(
			webhookEnabled: true,
			managementEndpoint: broker.ManagementEndpoint.AbsoluteUri,
			authRequired: false,
			authSecret: null,
			webhookIpSafelistEnabled: true,
			webhookAllowedNetworks: ["203.0.113.0/24"],
			forwardedHeadersEnabled: true,
			knownProxies: ["127.0.0.1", "::1"]);
		using var factory = new GatewayWebApplicationFactory();
		using var http = factory.CreateClient();
		using var request = CreateWebhookRequest(
			new StringContent("{}", Encoding.UTF8, "application/json"));
		request.Headers.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.25");

		using var response = await http.SendAsync(request);

		Assert.AreEqual(HttpStatusCode.Accepted, response.StatusCode);
		Assert.AreEqual(1, broker.CallCount, "A trusted proxy must preserve the allowed original client address.");
	}

	[TestMethod]
	public void CreateClient_EnabledSafelistWithoutNetworks_FailsStartup()
	{
		using var environment = ConfigureEnvironment(
			webhookEnabled: true,
			managementEndpoint: "http://127.0.0.1:1",
			authRequired: false,
			authSecret: null,
			webhookIpSafelistEnabled: true);
		using var factory = new GatewayWebApplicationFactory();

		OptionsValidationException? startupException = null;
		try
		{
			using var http = factory.CreateClient();
		}
		catch (OptionsValidationException exception)
		{
			startupException = exception;
		}

		Assert.IsNotNull(startupException);
		StringAssert.Contains(
			startupException.ToString(),
			"requires at least one allowed network",
			StringComparison.Ordinal);
	}

	[TestMethod]
	public void CreateClient_NonCanonicalSafelistNetwork_FailsStartup()
	{
		using var environment = ConfigureEnvironment(
			webhookEnabled: true,
			managementEndpoint: "http://127.0.0.1:1",
			authRequired: false,
			authSecret: null,
			webhookIpSafelistEnabled: true,
			webhookAllowedNetworks: ["10.0.0.1/24"]);
		using var factory = new GatewayWebApplicationFactory();

		OptionsValidationException? startupException = null;
		try
		{
			using var http = factory.CreateClient();
		}
		catch (OptionsValidationException exception)
		{
			startupException = exception;
		}

		Assert.IsNotNull(startupException);
		StringAssert.Contains(
			startupException.ToString(),
			"contains non-canonical CIDR network '10.0.0.1/24'. Use '10.0.0.0/24'",
			StringComparison.Ordinal);
	}

	[TestMethod]
	public void CreateClient_InvalidWebhookRateLimit_FailsStartup()
	{
		using var environment = ConfigureEnvironment(
			webhookEnabled: true,
			managementEndpoint: "http://127.0.0.1:1",
			authRequired: false,
			authSecret: null,
			webhookTokenLimit: 0,
			webhookTokensPerPeriod: 1);
		using var factory = new GatewayWebApplicationFactory();

		OptionsValidationException? startupException = null;
		try
		{
			using var http = factory.CreateClient();
		}
		catch (OptionsValidationException exception)
		{
			startupException = exception;
		}

		Assert.IsNotNull(startupException);
		StringAssert.Contains(
			startupException.ToString(),
			"Webhook:TokenLimit must be greater than zero",
			StringComparison.Ordinal);
	}

	private static HttpRequestMessage CreateWebhookRequest(HttpContent content)
	{
		return new HttpRequestMessage(HttpMethod.Post, WebhookUri)
		{
			Version = HttpVersion.Version11,
			VersionPolicy = HttpVersionPolicy.RequestVersionExact,
			Content = content,
		};
	}

	private static ProcessEnvironmentScope ConfigureEnvironment(
		bool webhookEnabled,
		string managementEndpoint,
		bool authRequired,
		string? authSecret,
		long maxBodyBytes = 1_048_576,
		int webhookTokenLimit = 30,
		int webhookTokensPerPeriod = 10,
		string webhookReplenishmentPeriod = "00:00:10",
		bool webhookIpSafelistEnabled = false,
		IReadOnlyList<string>? webhookAllowedNetworks = null,
		bool forwardedHeadersEnabled = false,
		IReadOnlyList<string>? knownProxies = null)
	{
		var settings = new Dictionary<string, string?>(StringComparer.Ordinal)
		{
			["DOTNET_ENVIRONMENT"] = "Development",
			["ASPNETCORE_ENVIRONMENT"] = "Development",
			["EnvironmentVariablesPrefix"] = "BURCINCO_",
			// Keep the in-process Prometheus exporter registered because production maps its endpoint,
			// but prevent the WAF process from exporting to an external collector.
			["OTEL_EXPORTER_OTLP_ENDPOINT"] = string.Empty,
			["BURCINCO_OTEL_EXPORTER_OTLP_ENDPOINT"] = string.Empty,
			["LOGGING__CONSOLE__FORMATTERNAME"] = null,
			["FeatureManagement__Gateway.Webhook"] = webhookEnabled.ToString(CultureInfo.InvariantCulture),
			["BURCINCO_FeatureManagement__Gateway.Webhook"] = webhookEnabled.ToString(CultureInfo.InvariantCulture),
			["ConnectionStrings__RabbitMqManagement"] = managementEndpoint,
			["BURCINCO_ConnectionStrings__RabbitMqManagement"] = managementEndpoint,
			["RabbitMq__ManagementConnectionStringKey"] = "RabbitMqManagement",
			["BURCINCO_RabbitMq__ManagementConnectionStringKey"] = "RabbitMqManagement",
			["Gateway__Webhook__VHost"] = "%2F",
			["BURCINCO_Gateway__Webhook__VHost"] = "%2F",
			["Gateway__Webhook__MaxBodyBytes"] = maxBodyBytes.ToString(CultureInfo.InvariantCulture),
			["BURCINCO_Gateway__Webhook__MaxBodyBytes"] = maxBodyBytes.ToString(CultureInfo.InvariantCulture),
			["Gateway__Webhook__PublishTimeout"] = "00:00:10",
			["BURCINCO_Gateway__Webhook__PublishTimeout"] = "00:00:10",
			["Gateway__WebhookAuth__Required"] = authRequired.ToString(CultureInfo.InvariantCulture),
			["BURCINCO_Gateway__WebhookAuth__Required"] = authRequired.ToString(CultureInfo.InvariantCulture),
			["Gateway__WebhookAuth__HeaderName"] = "X-Webhook-Secret",
			["BURCINCO_Gateway__WebhookAuth__HeaderName"] = "X-Webhook-Secret",
			["Gateway__WebhookAuth__Secret"] = authSecret,
			["BURCINCO_Gateway__WebhookAuth__Secret"] = authSecret,
			["Gateway__RateLimiting__Webhook__TokenLimit"] = webhookTokenLimit.ToString(CultureInfo.InvariantCulture),
			["BURCINCO_Gateway__RateLimiting__Webhook__TokenLimit"] = webhookTokenLimit.ToString(CultureInfo.InvariantCulture),
			["Gateway__RateLimiting__Webhook__TokensPerPeriod"] = webhookTokensPerPeriod.ToString(CultureInfo.InvariantCulture),
			["BURCINCO_Gateway__RateLimiting__Webhook__TokensPerPeriod"] = webhookTokensPerPeriod.ToString(CultureInfo.InvariantCulture),
			["Gateway__RateLimiting__Webhook__ReplenishmentPeriod"] = webhookReplenishmentPeriod,
			["BURCINCO_Gateway__RateLimiting__Webhook__ReplenishmentPeriod"] = webhookReplenishmentPeriod,
			["Gateway__NetworkSecurity__IpSafelists__gateway-webhook-ip-safelist__Enabled"] = webhookIpSafelistEnabled.ToString(CultureInfo.InvariantCulture),
			["BURCINCO_Gateway__NetworkSecurity__IpSafelists__gateway-webhook-ip-safelist__Enabled"] = webhookIpSafelistEnabled.ToString(CultureInfo.InvariantCulture),
			["Gateway__NetworkSecurity__ForwardedHeaders__Enabled"] = forwardedHeadersEnabled.ToString(CultureInfo.InvariantCulture),
			["BURCINCO_Gateway__NetworkSecurity__ForwardedHeaders__Enabled"] = forwardedHeadersEnabled.ToString(CultureInfo.InvariantCulture),
			["Gateway__NetworkSecurity__ForwardedHeaders__ForwardLimit"] = "1",
			["BURCINCO_Gateway__NetworkSecurity__ForwardedHeaders__ForwardLimit"] = "1",
			["ReverseProxy__Clusters__host__HealthCheck__Active__Enabled"] = "false",
			["BURCINCO_ReverseProxy__Clusters__host__HealthCheck__Active__Enabled"] = "false",
			["ReverseProxy__Clusters__portal__HealthCheck__Active__Enabled"] = "false",
			["BURCINCO_ReverseProxy__Clusters__portal__HealthCheck__Active__Enabled"] = "false",
		};

		AddMirroredArraySettings(
			settings,
			"Gateway__NetworkSecurity__IpSafelists__gateway-webhook-ip-safelist__AllowedNetworks",
			webhookAllowedNetworks);
		AddMirroredArraySettings(
			settings,
			"Gateway__NetworkSecurity__ForwardedHeaders__KnownProxies",
			knownProxies);

		return ProcessEnvironmentScope.Apply(settings);
	}

	private static void AddMirroredArraySettings(
		Dictionary<string, string?> settings,
		string key,
		IReadOnlyList<string>? values)
	{
		if (values is null)
		{
			return;
		}

		for (var index = 0; index < values.Count; index++)
		{
			settings[$"{key}__{index}"] = values[index];
			settings[$"BURCINCO_{key}__{index}"] = values[index];
		}
	}

}
