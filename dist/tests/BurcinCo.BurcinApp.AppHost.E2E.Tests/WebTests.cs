using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Aspire.Hosting;
using Aspire.Hosting.Testing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BurcinCo.BurcinApp.AppHost.E2E.Tests;

/// <summary>
/// Thin Aspire orchestration and public-boundary coverage. Process-local behavior belongs to the Host and Gateway
/// WebApplicationFactory integration projects; this suite proves AppHost startup and Gateway-to-Host routing.
/// </summary>
[TestClass]
[TestCategory("E2E")]
[DoNotParallelize]
public sealed class WebTests
{
	private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

	[TestMethod]
	public async Task Host_StartsThroughAspire_AndHealthEndpointResponds()
	{
		await using var app = await StartAppAsync();
		using var http = await CreateHttpClientAsync(app, "host");

		using var response = await http.GetAsync(new Uri("/healthz/live", UriKind.Relative));

		Assert.AreEqual(
			HttpStatusCode.OK,
			response.StatusCode,
			$"/healthz/live should be 200 when AppHost starts the Host. Got {response.StatusCode}.");
	}

#if (Web)
	[TestMethod]
	public async Task ClientWeb_StartsThroughAspire_AndHealthEndpointResponds()
	{
		await using var app = await StartAppAsync();
		using var http = await CreateHttpClientAsync(app, "client-web");

		using var response = await http.GetAsync(new Uri("/healthz/live", UriKind.Relative));

		Assert.AreEqual(
			HttpStatusCode.OK,
			response.StatusCode,
			$"/healthz/live should be 200 when AppHost starts Client.Web. Got {response.StatusCode}.");
	}

	[TestMethod]
	public async Task GetPortal_ThroughGateway_RendersSharedClientSurface()
	{
		await using var app = await StartAppAsync();
		await app.ResourceNotifications
			.WaitForResourceAsync("client-web", "Running")
			.WaitAsync(DefaultTimeout);
		using var http = await CreateHttpClientAsync(app, "gateway");

		using var response = await http.GetAsync(new Uri("/portal/", UriKind.Relative));
		var body = await response.Content.ReadAsStringAsync();

		Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, body);
		StringAssert.Contains(body, "<base href=\"/portal/\"", StringComparison.Ordinal);
		StringAssert.Contains(body, "data-client-surface=\"shared\"", StringComparison.Ordinal);
	}
#endif

	[TestMethod]
	public async Task GetPing_ThroughGateway_ReturnsExactPong()
	{
		await using var app = await StartAppAsync();
		using var http = await CreateGatewayHttpClientAsync(app);

		using var response = await http.GetAsync(new Uri("/ping", UriKind.Relative));

		Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
		Assert.AreEqual("text/plain", response.Content.Headers.ContentType?.MediaType);
		Assert.AreEqual("pong", await response.Content.ReadAsStringAsync());
	}

	[TestMethod]
	public async Task GetMe_UnauthenticatedThroughGateway_ReturnsUnauthorizedWithBearerChallenge()
	{
		await using var app = await StartAppAsync();
		using var http = await CreateGatewayHttpClientAsync(app);

		using var response = await http.GetAsync(new Uri("/me", UriKind.Relative));

		Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
		Assert.AreEqual("Bearer", response.Headers.WwwAuthenticate.ToString());
	}

	private static async Task<DistributedApplication> StartAppAsync()
	{
		var appHost = await DistributedApplicationTestingBuilder
			.CreateAsync<Projects.BurcinCo_BurcinApp_AppHost>(
				["--environment=Development", "--Logging:EventLog:LogLevel:Default=None"]);
		appHost.Services.AddLogging(logging =>
		{
			logging.SetMinimumLevel(LogLevel.Debug);
			logging.AddFilter(appHost.Environment.ApplicationName, LogLevel.Debug);
			logging.AddFilter("BurcinCo.", LogLevel.Debug);
		});

		var app = await appHost.BuildAsync().WaitAsync(DefaultTimeout);
		await app.StartAsync().WaitAsync(DefaultTimeout);
		return app;
	}

	private static async Task<HttpClient> CreateGatewayHttpClientAsync(DistributedApplication app)
	{
		await app.ResourceNotifications
			.WaitForResourceAsync("host", "Running")
			.WaitAsync(DefaultTimeout);
		return await CreateHttpClientAsync(app, "gateway");
	}

	private static async Task<HttpClient> CreateHttpClientAsync(DistributedApplication app, string resourceName)
	{
		await app.ResourceNotifications
			.WaitForResourceAsync(resourceName, "Running")
			.WaitAsync(DefaultTimeout);

		var http = app.CreateHttpClient(resourceName);
		http.Timeout = DefaultTimeout;
		return http;
	}
}
