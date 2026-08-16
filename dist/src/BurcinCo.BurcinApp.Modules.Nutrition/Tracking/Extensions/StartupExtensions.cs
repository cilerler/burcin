using System;
using BurcinCo.BurcinApp.Modules.Nutrition.Tracking.NutritionFact.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace BurcinCo.BurcinApp.Modules.Nutrition.Tracking.Extensions;

public static class StartupExtensions
{
	public static IServiceCollection AddTrackingComponent(
		this IServiceCollection services,
		bool recipeIsLocal)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.AddNutritionFactService(recipeIsLocal);

		return services;
	}
}
