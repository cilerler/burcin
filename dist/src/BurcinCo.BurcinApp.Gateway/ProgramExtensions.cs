using System;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.FeatureFilters;
using Ruya.AspNetCore.Diagnostics.GlobalExceptionHandler;
using Ruya.AspNetCore.Middleware.AppEnvironmentResponseHeaders;
using Ruya.Diagnostics.DistributedTracing;
using Ruya.Extensions.Configuration;
using Ruya.OpenTelemetry;
using Ruya.Primitives;

namespace BurcinCo.BurcinApp.Gateway;

/// <summary>
/// Template-default startup wiring — the bits that look the same across every deployable in this
/// shop (Host, Gateway, any future composition root). Anything project-specific lives in
/// <see cref="ProgramExtensionsCustom"/>. Keeping this split regen-friendly: a future template
/// refresh shouldn't clobber a project's distinct wiring.
/// </summary>
internal static class ProgramExtensions
{
	//! do not change the order of these calls without understanding their dependencies.

	public static IHostApplicationBuilder AddDefaultServices(this IHostApplicationBuilder builder)
	{
		builder.Configuration.AddKubernetesConfiguration();
		builder.Configuration.AddEnvironmentVariablesWithPrefix();

		builder.Services.AddGlobalExceptionHandlerService();

		builder.Services.AddMemoryCache();
		builder.Services.AddDistributedMemoryCache();

		// Observability: OpenTelemetry (logs/metrics/traces) + distributed tracing abstraction.
		// Service Discovery — drives both Aspire local-dev and aspire-publish compose + K8s via
		// services__<name>__<scheme>__<index> env vars.

		builder.Services.AddHybridCache();

		builder.Services.AddSingleton(TimeProvider.System);

		builder.Services.AddHttpContextAccessor();
		builder.Services.AddServiceDiscovery();

		builder.Services.AddHttpClient();
		builder.Services.ConfigureHttpClientDefaults(http =>
		{
			http.AddServiceDiscovery();
			//http.AddStandardResilienceHandler();
		});

		builder.ConfigureOpenTelemetry();
		builder.Services.AddDistributedTracingService();

		builder.Services.AddFeatureManagement(builder.Configuration.GetSection(FeatureFlags.ConfigurationSectionName))
			//.AddFeatureFilter<TargetingFilter>()
			.AddFeatureFilter<PercentageFilter>()
			.AddFeatureFilter<TimeWindowFilter>();

		builder.Services.AddAppEnvironmentResponseHeaders();

		// Kestrel endpoints are driven by Aspire via ASPNETCORE_URLS + dev-cert (or
		// Kestrel:Certificates:Default config in prod). Do NOT override with ConfigureKestrel
		// here — it silently disables Aspire's 443 binding when no cert path is present.

		builder.Services.AddHostedService<StartupBackgroundService>();
		builder.Services.AddSingleton<StartupHealthCheck>();
		builder.Services.AddResourceMonitoring();
		builder.Services.AddHealthChecks()
						.AddResourceUtilizationHealthCheck()
						.AddApplicationLifecycleHealthCheck()
						.AddCheck<StartupHealthCheck>("Startup", tags: ["startup"])
			;

		return builder;
	}

	//! do not change the order of these calls without understanding their dependencies.
	public static WebApplication ConfigureDefaultPipeline(this WebApplication app)
	{
		app.UseForwardedHeaders();

		if (app.Environment.IsDevelopment())
		{
			app.UseDeveloperExceptionPage();
		}
		else
		{
			app.UseExceptionHandler();
			app.UseHsts();
			app.UseHttpsRedirection();
		}

		app.UseRouting();

		app.MapPrometheusScrapingEndpoint();

		// Health check endpoints (live/ready/startup triad per melis observability skill).
		var liveOptions = new HealthCheckOptions { Predicate = _ => false };
		var readyOptions = new HealthCheckOptions { Predicate = h => h.Tags.Contains("ready") };
		var startupOptions = new HealthCheckOptions { Predicate = h => h.Tags.Contains("startup") };
		var healthGroup = app.MapGroup("");
		healthGroup.MapHealthChecks("/health");
		healthGroup.MapHealthChecks("/healthz", readyOptions);
		healthGroup.MapHealthChecks("/healthz/ready", readyOptions);
		healthGroup.MapHealthChecks("/healthz/live", liveOptions);
		healthGroup.MapHealthChecks("/healthz/startup", startupOptions);

		app.UseMiddlewareForFeature<AppEnvironmentResponseHeadersMiddleware>(AppEnvironmentResponseHeadersSettings.FeatureFlag);

		return app;
	}
}
