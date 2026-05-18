using System;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Procurement.Extensions;

public static class StartupExtensions
{
	public static IServiceCollection AddProcurementComponent(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		services.AddIngredientSupplyService(configuration);

		return services;
	}

	public static IEndpointRouteBuilder MapProcurementComponent(this IEndpointRouteBuilder endpoints)
	{
		ArgumentNullException.ThrowIfNull(endpoints);

		endpoints.MapIngredientSupplyApi();

		return endpoints;
	}
}
