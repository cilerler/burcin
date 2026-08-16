using System;

using BurcinCo.BurcinApp.Gateway.Configuration;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;

namespace BurcinCo.BurcinApp.Gateway.Integration.Tests.Fixtures;

internal sealed class GatewayWebApplicationFactory : WebApplicationFactory<CapabilitySelection>
{
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);
		// Production logging providers are outside this process-local HTTP test boundary.
		builder.ConfigureLogging(logging => logging.ClearProviders());
	}
}
