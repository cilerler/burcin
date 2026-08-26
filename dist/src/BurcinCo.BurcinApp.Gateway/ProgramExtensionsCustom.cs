using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BurcinCo.BurcinApp.Gateway.Configuration;
using BurcinCo.BurcinApp.Gateway.NetworkSecurity.Extensions;
using BurcinCo.BurcinApp.Gateway.RateLimiting.Extensions;
using BurcinCo.BurcinApp.Gateway.ReverseProxy.Api;
#if (Sample)
using BurcinCo.BurcinApp.Gateway.Webhook.Extensions;
#endif
using ReverseProxyConstants = BurcinCo.BurcinApp.Gateway.ReverseProxy.Constants;

namespace BurcinCo.BurcinApp.Gateway;

/// <summary>
/// Gateway's distinct wiring — what differs from every other deployable in this shop.
/// It composes the YARP reverse proxy, per-client rate limits, configurable CIDR safelists, and
/// trusted forwarded-address handling. Destinations resolve through Service Discovery so the same
/// routing configuration works in Aspire local development, Docker Compose, and Kubernetes.
/// When the Sample is selected, it also receives supplier Webhooks and hands them to the configured
/// Gateway edge adapter (RabbitMQ in the reference application).
/// </summary>
internal static class ProgramExtensionsCustom
{
	public static IHostApplicationBuilder AddCustomServices(
		this IHostApplicationBuilder builder,
		CapabilitySelection capabilities)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(capabilities);
		builder.Services.AddSingleton(capabilities);
		builder.Services.AddGatewayNetworkSecurity(builder.Configuration);
		builder.Services.AddGatewayRateLimiting(builder.Configuration);

#if (Sample)
		// Webhook ingestion. The Gateway edge capability owns its settings (WebhookSettings,
		// WebhookAuthSettings, and RabbitMqSettings), endpoint filter, typed HttpClient, and the
		// PublishAsync implementation under Gateway/Webhook.
		if (capabilities.Webhook)
		{
			builder.Services.AddWebhook();
		}
#endif

		// YARP route and destination configuration is process-specific, so the Gateway composition
		// root owns it directly instead of passing IConfiguration through a service registration API.
		builder.Services.AddReverseProxy()
			.LoadFromConfig(builder.Configuration.GetRequiredSection(
				ReverseProxyConstants.ConfigurationSections.ReverseProxy))
			.AddServiceDiscoveryDestinationResolver();

		return builder;
	}

	public static WebApplication ConfigureCustomPipeline(this WebApplication app)
	{

#if (Sample)
		// Webhook routes. WebhookApi owns the path shape (/webhooks/{**path}) and the
		// shared-secret filter wiring; the implementation is intrinsic to this Gateway process.
		var capabilities = app.Services.GetRequiredService<CapabilitySelection>();
		app.MapWebhook(capabilities.Webhook);
#endif

		// Reverse-proxy mapping. In Sample output it comes after the Webhook routes so explicit endpoints win
		// when paths overlap. Wrapper is suffixed "Api" to avoid collision with YARP's own
		// MapReverseProxy extension on IEndpointRouteBuilder.
		app.MapReverseProxyApi();

		return app;
	}
}
