using Microsoft.AspNetCore.Builder;

namespace BurcinCo.BurcinApp.Gateway.Extensions;

internal static class StartupExtensionsCustom
{
	// User extension point: add gateway-specific services here without touching template defaults.
	public static WebApplicationBuilder AddCustomServices(this WebApplicationBuilder builder)
	{
		return builder;
	}

	// User extension point: add gateway-specific pipeline steps here without touching template defaults.
	public static WebApplication ConfigureCustomPipeline(this WebApplication app)
	{
		return app;
	}
}
