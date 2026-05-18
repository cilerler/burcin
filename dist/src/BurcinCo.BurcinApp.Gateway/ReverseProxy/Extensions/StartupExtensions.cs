using System;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BurcinCo.BurcinApp.Gateway.ReverseProxy.Extensions;

/// <summary>
/// DI registration for Gateway's ReverseProxy feature. Wraps YARP's
/// <c>AddReverseProxy().LoadFromConfig().AddServiceDiscoveryDestinationResolver()</c> chain so the
/// composition root's <c>ProgramExtensionsCustom</c> only needs to call
/// <c>AddReverseProxyService(config)</c>. Mirrors the <c>AddWebhookService</c> shape so both
/// Gateway-level features have parallel call sites.
/// </summary>
internal static class StartupExtensions
{
	public static IServiceCollection AddReverseProxyService(this IServiceCollection services, IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		// YARP loads route + cluster config from the "ReverseProxy" section and resolves destination
		// addresses via Service Discovery (the same provider Gateway's ProgramExtensions wires up).
		services.AddReverseProxy()
			.LoadFromConfig(configuration.GetSection(Constants.ConfigurationSections.ReverseProxy))
			.AddServiceDiscoveryDestinationResolver();

		return services;
	}
}
