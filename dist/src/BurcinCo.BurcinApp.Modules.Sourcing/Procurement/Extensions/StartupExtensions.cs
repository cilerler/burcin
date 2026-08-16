using System;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Procurement.Extensions;

public static class StartupExtensions
{
	public static IServiceCollection AddProcurementComponent(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.AddIngredientSupply();

		return services;
	}

	public static WebApplication MapProcurementComponent(
		this WebApplication app,
		bool enabled)
	{
		ArgumentNullException.ThrowIfNull(app);

		if (!enabled)
		{
			return app;
		}

		app.MapIngredientSupply(enabled);

		return app;
	}
}
