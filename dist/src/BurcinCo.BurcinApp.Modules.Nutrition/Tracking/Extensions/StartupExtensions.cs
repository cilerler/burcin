using System;
using BurcinCo.BurcinApp.Modules.Nutrition.Tracking.NutritionFact.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BurcinCo.BurcinApp.Modules.Nutrition.Tracking.Extensions;

public static class StartupExtensions
{
	public static IServiceCollection AddTrackingComponent(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		services.AddNutritionFactService(configuration);

		return services;
	}
}
