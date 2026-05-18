using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BurcinCo.BurcinApp.Gateway.ReverseProxy.Api;
using BurcinCo.BurcinApp.Gateway.ReverseProxy.Extensions;
using BurcinCo.BurcinApp.Gateway.Webhook.Api;
using BurcinCo.BurcinApp.Gateway.Webhook.Extensions;

namespace BurcinCo.BurcinApp.Gateway;

/// <summary>
/// Gateway's distinct wiring — what differs from every other deployable in this shop. Two jobs:
///   1. Webhook ingestion: receive supplier webhook callbacks and deliver them to a configurable
///      sink (today: RabbitMQ; alternative transports like MSSQL are a sink-strategy swap inside
///      the Webhook service). Wired via the Webhook service's own <c>AddWebhookService</c> extension.
///   2. Reverse proxy: forward incoming requests to module-deployment backends through YARP.
///      Destinations resolve via Service Discovery so the same routing config works in Aspire
///      local-dev, docker compose, and K8s.
/// </summary>
internal static class ProgramExtensionsCustom
{
	public static IHostApplicationBuilder AddCustomServices(this IHostApplicationBuilder builder)
	{

		// (1) Webhook ingestion. The service owns its own settings (WebhookSettings,
		// WebhookAuthSettings, RabbitMqSettings), endpoint filter, typed HttpClient, and the
		// PublishAsync implementation — all in dist/src/.../Gateway/Webhook/.
		builder.Services.AddWebhookService(builder.Configuration);

		// (2) Reverse proxy. The feature owns its YARP wiring (config-section loader + service-discovery
		// destination resolver) — all in dist/src/.../Gateway/ReverseProxy/.
		builder.Services.AddReverseProxyService(builder.Configuration);

		return builder;
	}

	public static WebApplication ConfigureCustomPipeline(this WebApplication app)
	{

		// (1) Webhook routes. WebhookApi owns the path shape (/webhooks/{**path}) and the
		// shared-secret filter wiring; the individual verb endpoints live in Webhook/Api/*.cs.
		app.MapWebhook();

		// (2) Reverse-proxy mapping. Comes after the Webhook routes so explicit endpoints win
		// when paths overlap. Wrapper is suffixed "Api" to avoid collision with YARP's own
		// MapReverseProxy extension on IEndpointRouteBuilder.
		app.MapReverseProxyApi();

		return app;
	}
}
