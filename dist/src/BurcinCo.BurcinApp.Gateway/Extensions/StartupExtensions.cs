using System;
using System.Net.Http.Headers;
using System.Text;

using BurcinCo.BurcinApp.Gateway.Api.Filters;
using BurcinCo.BurcinApp.Gateway.Configuration;
using BurcinCo.BurcinApp.Gateway.Contracts;
using BurcinCo.BurcinApp.Gateway.Services;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Ruya.Diagnostics.DistributedTracing;
using Ruya.Extensions.Configuration;
using Ruya.OpenTelemetry;

namespace BurcinCo.BurcinApp.Gateway.Extensions;

internal static class StartupExtensions
{
	//! do not change the order of these calls without understanding their dependencies.

	public static WebApplicationBuilder AddDefaultServices(this WebApplicationBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Configuration.AddKubernetesConfiguration();
		builder.Configuration.AddEnvironmentVariablesWithPrefix();

		// Settings
		builder.Services.AddOptions<WebhookServiceSettings>()
			.BindConfiguration(WebhookServiceSettings.ConfigurationSectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		builder.Services.AddOptions<WebhookAuthSettings>()
			.BindConfiguration(WebhookAuthSettings.ConfigurationSectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		builder.Services.AddOptions<RabbitMqSettings>()
			.BindConfiguration(RabbitMqSettings.ConfigurationSectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// IDistributedCache is required by Ruya.Diagnostics.DistributedTracing.
		// Use the in-memory implementation unless/until multi-instance trace correlation is needed.
		builder.Services.AddDistributedMemoryCache();

		// Observability: OpenTelemetry (logs/metrics/traces) + distributed tracing abstraction.
		builder.ConfigureOpenTelemetry();
		builder.Services.AddDistributedTracingService();

		// HTTP clients
		builder.Services.AddHttpClient();
		builder.Services.AddHttpClient(Constants.HttpClients.RabbitMqManagement, (sp, client) =>
		{
			var settings = sp.GetRequiredService<IOptions<RabbitMqSettings>>().Value;
			var configuration = sp.GetRequiredService<IConfiguration>();
			var managementUrl = configuration.GetConnectionString(settings.ManagementConnectionStringKey)
				?? throw new InvalidOperationException(
					$"ConnectionStrings:{settings.ManagementConnectionStringKey} is not configured.");

			var uri = new Uri(managementUrl);
			// BaseAddress must not include userinfo; strip before assigning, capture for Basic auth.
			client.BaseAddress = new UriBuilder(uri) { UserName = string.Empty, Password = string.Empty }.Uri;
			if (!string.IsNullOrEmpty(uri.UserInfo))
			{
				client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
					"Basic",
					Convert.ToBase64String(Encoding.UTF8.GetBytes(Uri.UnescapeDataString(uri.UserInfo))));
			}
		});

		// Services
		builder.Services.AddSingleton<IWebhookService, WebhookService>();
		builder.Services.AddSingleton<WebhookSecretAuthFilter>();

		// Service Discovery (drives both Aspire local-dev and aspire-publish compose + K8s, via services__<name>__<scheme>__<index> env vars).
		builder.Services.AddServiceDiscovery();

		// Reverse proxy — uses Service Discovery to resolve destination addresses (e.g. https+http://host).
		builder.Services.AddReverseProxy()
			.LoadFromConfig(builder.Configuration.GetSection(Constants.ConfigurationSections.ReverseProxy))
			.AddServiceDiscoveryDestinationResolver();

		// Kestrel endpoints are driven by Aspire via ASPNETCORE_URLS + dev-cert (or Kestrel:Certificates:Default config in prod).
		// Do not override with ConfigureKestrel here — it silently disables Aspire's 443 binding when no cert path is present in config.

		// Health checks
		builder.Services.AddHealthChecks();

		return builder;
	}

	//! do not change the order of these calls without understanding their dependencies.
	public static WebApplication ConfigureDefaultPipeline(this WebApplication app)
	{
		ArgumentNullException.ThrowIfNull(app);

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
		var healthGroup = app.MapGroup(string.Empty);
		healthGroup.MapHealthChecks("/health");
		healthGroup.MapHealthChecks("/healthz");
		healthGroup.MapHealthChecks("/healthz/live", liveOptions);
		healthGroup.MapHealthChecks("/healthz/ready");
		healthGroup.MapHealthChecks("/healthz/startup");

		app.MapWebhookEndpoints();
		app.MapReverseProxy();

		return app;
	}
}
