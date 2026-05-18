using System;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Interfaces;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Clients;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Configuration;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Contracts;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Handlers;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Workers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Extensions;

public static class StartupExtensions
{
	public static IServiceCollection AddIngredientSupplyService(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		services.AddOptions<IngredientSupplySettings>()
			.BindConfiguration(IngredientSupplySettings.ConfigurationSectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.AddOptions<SupplierWebhookClientSettings>()
			.BindConfiguration(SupplierWebhookClientSettings.ConfigurationSectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.TryAddSingleton(TimeProvider.System);

		// Producer / read service. Implements both the internal IIngredientSupplyService (used by
		// the local Api.cs) and the public ISourcingService (used cross-module).
		services.AddScoped<IngredientSupplyService>();
		services.AddScoped<IIngredientSupplyService>(sp => sp.GetRequiredService<IngredientSupplyService>());
		services.AddScoped<ISourcingService>(sp => sp.GetRequiredService<IngredientSupplyService>());

		// Inbox handler — Scoped so each message gets a fresh DbContext.
		services.AddScoped<QuoteResponseHandler>();

		// Outbound HTTP client — convention-located in this service's Clients/ folder.
		services.AddHttpClient<SupplierWebhookClient>();
		services.AddScoped<SupplierWebhookClient>();

		// Workers: one consumes the internal Outbox topic and dispatches to the supplier (outbound);
		// the other consumes the Gateway-published webhook topic and runs Inbox-deduped handler (inbound).
		services.AddHostedService<QuoteRequestDispatcher>();
		services.AddHostedService<QuoteResponseSubscriber>();

		return services;
	}

	public static IEndpointRouteBuilder MapIngredientSupplyApi(this IEndpointRouteBuilder endpoints)
	{
		ArgumentNullException.ThrowIfNull(endpoints);
		return IngredientSupplyApi.Map(endpoints);
	}
}
