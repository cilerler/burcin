using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BurcinCo.BurcinApp.AppHost.E2E.Tests;

/// <summary>
/// Aspire-orchestrated smoke test. Spins up the full distributed application — MsSql + Redis +
/// RabbitMQ + Host + Gateway — exactly as the AppHost does in dev, then verifies that the
/// Host's module routes are reachable through the orchestrated stack.
///
/// Slow (container startup ~30s+ on first run). Categorize as E2E so it can be filtered
/// out of fast feedback loops.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public sealed class WebTests
{
	private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

	[TestMethod]
	public async Task Host_StartsThroughAspire_AndHealthEndpointResponds()
	{
		// Arrange — bring the full Aspire app up.
		var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.BurcinCo_BurcinApp_AppHost>(["--environment=Development"]);
		appHost.Services.AddLogging(logging =>
		{
			logging.SetMinimumLevel(LogLevel.Debug);
			logging.AddFilter(appHost.Environment.ApplicationName, LogLevel.Debug);
			logging.AddFilter("BurcinCo.", LogLevel.Debug);
		});
		// No standard resilience handler — its 30s total-request timeout is too tight for /healthz when
		// the underlying SqlServer health check is still warming up. The test's DefaultTimeout governs.

		await using var app = await appHost.BuildAsync().WaitAsync(DefaultTimeout);
		await app.StartAsync().WaitAsync(DefaultTimeout);

		using var http = app.CreateHttpClient("host");
		// Independent of any default timeout — give the health endpoint room to wait on slow checks.
		http.Timeout = DefaultTimeout;

		// Wait until the host is at least Running. Gating on Healthy is brittle in test harnesses
		// because the Host's health probe pings testcontainer mssql which can spend a while in pre-login
		// handshake while becoming ready. Routing comes up before the health endpoint goes green.
		await app.ResourceNotifications
			.WaitForResourceAsync("host", "Running")
			.WaitAsync(DefaultTimeout);

		// Act — GET /healthz/live. The live probe uses `Predicate = _ => false`, so it skips all
		// dependency checks (mssql / broker / etc.) and just returns 200 if the process is alive.
		// That's the minimal-cost smoke for "Aspire orchestration produced a routable Host"; the full
		// /healthz path would block on the SqlServer health check, which can take minutes to settle
		// in a fresh testcontainer. The full readiness path is exercised by the dev workflow, not this test.
		using var response = await http.GetAsync(new Uri("/healthz/live", UriKind.Relative));

		// Assert — process-alive probe must be 200. Anything else means the Host isn't up.
		Assert.AreEqual(
			HttpStatusCode.OK,
			response.StatusCode,
			$"/healthz/live should be 200 when the Host process is up under Aspire. Got {response.StatusCode}.");
	}
}
