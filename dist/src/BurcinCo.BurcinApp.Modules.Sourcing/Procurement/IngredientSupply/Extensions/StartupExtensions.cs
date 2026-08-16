using System;
using System.Diagnostics.Metrics;
using System.Threading;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Interfaces;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Api;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Clients;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Configuration;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Contracts;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using Ruya.Diagnostics.DistributedTracing;
using Ruya.Extensions.DependencyInjection;
using Ruya.Primitives;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Extensions;

public static class StartupExtensions
{
	public static IServiceCollection AddIngredientSupply(
		this IServiceCollection services,
		Action<IngredientSupplySettings>? setupAction = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		services.EnsureServicesRegistered(
			typeof(IDistributedTracing),
			typeof(IMeterFactory));

		services.AddOptions<IngredientSupplySettings>()
			.BindConfiguration(IngredientSupplySettings.ConfigurationSectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		if (setupAction is not null)
		{
			services.Configure(setupAction);
		}

		services.AddOptions<SupplierWebhookClientSettings>()
			.BindConfiguration(SupplierWebhookClientSettings.ConfigurationSectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.ConfigureHttpJsonOptions(options =>
		{
			options.SerializerOptions.TypeInfoResolverChain.Insert(
				0,
				IngredientSupplyJsonSerializerContext.Default);
		});

		services.TryAddSingleton(TimeProvider.System);

		// Producer / read service. Implements both the internal IIngredientSupply (used by
		// the local Api.cs) and the public ISourcingService (used cross-module).
		services.AddScoped<IngredientSupplyService>();
		services.AddScoped<IIngredientSupply>(sp => sp.GetRequiredService<IngredientSupplyService>());
		services.AddScoped<ISourcingService>(sp => sp.GetRequiredService<IngredientSupplyService>());

		// Supplier endpoints are arbitrary external systems, so an Idempotency-Key header alone is not
		// proof that a remote endpoint enforces idempotency. Do not stack an automatic HTTP retry on top
		// of the subscriber's finite broker-redelivery policy. Circuit breaking and timeout remain safe.
		services.AddHttpClient(
				Constants.HttpClients.SupplierWebhook,
				client => client.Timeout = Timeout.InfiniteTimeSpan)
			.AddResilienceHandler(
				Constants.ResiliencePipelines.SupplierWebhook,
				static (pipeline, context) =>
				{
					var settings = context.ServiceProvider
						.GetRequiredService<IOptions<SupplierWebhookClientSettings>>()
						.Value;
					pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
					{
						SamplingDuration = TimeSpan.FromSeconds(90),
						FailureRatio = 0.2,
						MinimumThroughput = 20,
					});
					pipeline.AddTimeout(settings.HttpTimeout);
				});

		services.AddScoped<SupplierWebhookClient>();

		// Subscribers: one delegates Outbox-dispatched quote requests to the service; the other delegates
		// Gateway-Webhook-published supplier responses through atomic Inbox processing to the same scoped service.
		services.AddHostedService<IngredientQuoteRequestedEventSubscriber>();
		services.AddHostedService<IngredientQuoteResponseReceivedEventSubscriber>();

		return services;
	}

	public static WebApplication MapIngredientSupply(
		this WebApplication app,
		bool enabled)
	{
		ArgumentNullException.ThrowIfNull(app);

		if (!enabled)
		{
			return app;
		}

		var serviceProviderIsService =
			app.Services.GetRequiredService<IServiceProviderIsService>();

		if (!serviceProviderIsService.IsService(typeof(IIngredientSupply)))
		{
			throw new InvalidOperationException(
				"Cannot map IngredientSupply endpoints because IIngredientSupply is not registered. " +
				"Run the Sourcing registration cascade before endpoint mapping.");
		}

		return app.MapIngredientSupplyApi();
	}
}
