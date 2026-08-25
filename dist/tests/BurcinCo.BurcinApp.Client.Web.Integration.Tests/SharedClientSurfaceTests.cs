using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BurcinCo.BurcinApp.Client.Web.Integration.Tests;

[TestClass]
[TestCategory("Integration")]
public sealed class SharedClientSurfaceTests
{
	[TestMethod]
	public async Task GetPortal_RendersSharedClientSurfaceWithPortalBasePath()
	{
		await using var baseFactory = new WebApplicationFactory<Program>();
		await using var factory = baseFactory
			.WithWebHostBuilder(builder =>
			{
				builder.UseEnvironment("Testing");
				builder.ConfigureLogging(logging =>
				{
					logging.ClearProviders();
					logging.AddConsole();
				});
				builder.ConfigureServices(services =>
					services.AddDataProtection().UseEphemeralDataProtectionProvider());
			});
		using var http = factory.CreateClient();

		using var response = await http.GetAsync(new Uri("/portal/", UriKind.Relative));
		var body = await response.Content.ReadAsStringAsync();

		Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, body);
		StringAssert.Contains(body, "<base href=\"/portal/\"", StringComparison.Ordinal);
		StringAssert.Contains(body, "data-client-surface=\"shared\"", StringComparison.Ordinal);
	}
}
