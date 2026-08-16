using System;
using System.Diagnostics.Metrics;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;

using BurcinCo.BurcinApp.Gateway.Webhook.Api;
using BurcinCo.BurcinApp.Gateway.Webhook.Api.Filters;
using BurcinCo.BurcinApp.Gateway.Webhook.Configuration;
using BurcinCo.BurcinApp.Gateway.Webhook.Contracts;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

using Polly;

using Ruya.Diagnostics.DistributedTracing;
using Ruya.Extensions.DependencyInjection;
using Ruya.Primitives;

namespace BurcinCo.BurcinApp.Gateway.Webhook.Extensions;

/// <summary>
/// Owns the Gateway process's Webhook edge-adapter registration graph without passing raw
/// configuration through the registration API.
/// </summary>
internal static class StartupExtensions
{
	public static IServiceCollection AddWebhook(
		this IServiceCollection services,
		Action<WebhookSettings>? setupAction = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		services.EnsureServicesRegistered(
			typeof(IDistributedTracing),
			typeof(IMeterFactory));

		services.AddOptions<WebhookSettings>()
			.BindConfiguration(WebhookSettings.ConfigurationSectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		if (setupAction is not null)
		{
			services.Configure(setupAction);
		}

		services.AddOptions<WebhookAuthSettings>()
			.BindConfiguration(WebhookAuthSettings.ConfigurationSectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.AddOptions<RabbitMqSettings>()
			.BindConfiguration(RabbitMqSettings.ConfigurationSectionName)
			.ValidateDataAnnotations()
			.Validate<IConfiguration>(
				static (settings, configuration) =>
					TryResolveManagementEndpoint(configuration, settings, out _),
				"The RabbitMQ management connection string must be configured as an absolute HTTP(S) URI.")
			.ValidateOnStart();

		services.TryAddSingleton(TimeProvider.System);
		services.AddSingleton<IWebhook, WebhookService>();
		services.AddSingleton<WebhookSecretAuthFilter>();

		services.AddHttpClient(Constants.HttpClients.RabbitMqManagement, (serviceProvider, client) =>
		{
			var settings = serviceProvider.GetRequiredService<IOptions<RabbitMqSettings>>().Value;
			var configuration = serviceProvider.GetRequiredService<IConfiguration>();
			if (!TryResolveManagementEndpoint(configuration, settings, out var endpoint))
			{
				throw new InvalidOperationException(
					$"ConnectionStrings:{settings.ManagementConnectionStringKey} must be configured as an absolute HTTP(S) URI.");
			}

			client.Timeout = Timeout.InfiniteTimeSpan;
			client.BaseAddress = new UriBuilder(endpoint) { UserName = string.Empty, Password = string.Empty }.Uri;
			if (!string.IsNullOrEmpty(endpoint.UserInfo))
			{
				client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
					"Basic",
					Convert.ToBase64String(Encoding.UTF8.GetBytes(Uri.UnescapeDataString(endpoint.UserInfo))));
			}
		})
		.AddResilienceHandler(
			Constants.ResiliencePipelines.RabbitMqManagement,
			static (pipeline, context) =>
			{
				var settings = context.ServiceProvider.GetRequiredService<IOptions<WebhookSettings>>().Value;
				// RabbitMQ's management publish endpoint has no idempotency-key contract, so this POST
				// deliberately has no automatic retry. Circuit breaking and timeout remain safe.
				pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions());
				pipeline.AddTimeout(settings.PublishTimeout);
			});

		return services;
	}

	public static WebApplication MapWebhook(this WebApplication app, bool enabled)
	{
		ArgumentNullException.ThrowIfNull(app);

		if (!enabled)
		{
			return app;
		}

		var serviceProviderIsService =
			app.Services.GetRequiredService<IServiceProviderIsService>();
		if (!serviceProviderIsService.IsService(typeof(IWebhook)))
		{
			throw new InvalidOperationException(
				"Cannot map Webhook endpoints because IWebhook is not registered. " +
				"Run the deployable registration cascade before endpoint mapping.");
		}

		return app.MapWebhookApi();
	}

	private static bool TryResolveManagementEndpoint(
		IConfiguration configuration,
		RabbitMqSettings settings,
		out Uri endpoint)
	{
		endpoint = null!;
		if (string.IsNullOrWhiteSpace(settings.ManagementConnectionStringKey))
		{
			return false;
		}

		var managementConnectionString =
			configuration.GetConnectionString(settings.ManagementConnectionStringKey);
		return Uri.TryCreate(managementConnectionString, UriKind.Absolute, out endpoint!)
			&& (endpoint.Scheme == Uri.UriSchemeHttp || endpoint.Scheme == Uri.UriSchemeHttps);
	}
}
