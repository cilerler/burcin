using System;
using BurcinCo.BurcinApp.Modules.Nutrition.Tracking.NutritionFact.Clients;
using BurcinCo.BurcinApp.Modules.Nutrition.Tracking.NutritionFact.Configuration;
using BurcinCo.BurcinApp.Modules.Nutrition.Tracking.NutritionFact.Contracts;
using BurcinCo.BurcinApp.Modules.Recipe.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace BurcinCo.BurcinApp.Modules.Nutrition.Tracking.NutritionFact.Extensions;

public static class StartupExtensions
{
	public static IServiceCollection AddNutritionFactService(
		this IServiceCollection services,
		bool recipeIsLocal)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.AddOptions<NutritionFactSettings>()
			.BindConfiguration(NutritionFactSettings.ConfigurationSectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.AddScoped<INutritionFactService, NutritionFactService>();

		// Cross-module wiring: bind IRecipeService to either local impl (already registered by
		// AddRecipeModule when Recipe is local) or the HTTP RecipeClient (when Recipe is remote).
		// The Host passes its captured module-selection decision through the registration cascade.
		// Do not re-read live feature configuration here: registration and mapping must share one graph.
		if (!recipeIsLocal)
		{
			services.AddOptions<RecipeClientSettings>()
				.BindConfiguration(RecipeClientSettings.ConfigurationSectionName)
				.ValidateDataAnnotations()
				.ValidateOnStart();

			services.AddHttpClient<IRecipeService, RecipeClient>((sp, http) =>
			{
				var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RecipeClientSettings>>().Value;
				if (!string.IsNullOrWhiteSpace(settings.BaseAddress))
				{
					http.BaseAddress = new System.Uri(settings.BaseAddress);
				}
				http.Timeout = System.TimeSpan.FromSeconds(settings.TimeoutSeconds);
			});
		}

		return services;
	}
}
