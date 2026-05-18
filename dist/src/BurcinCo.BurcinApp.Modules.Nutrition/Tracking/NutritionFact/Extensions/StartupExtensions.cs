using System;
using BurcinCo.BurcinApp.Modules.Nutrition.Tracking.NutritionFact.Clients;
using BurcinCo.BurcinApp.Modules.Nutrition.Tracking.NutritionFact.Configuration;
using BurcinCo.BurcinApp.Modules.Nutrition.Tracking.NutritionFact.Contracts;
using BurcinCo.BurcinApp.Modules.Recipe.Abstractions.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BurcinCo.BurcinApp.Modules.Nutrition.Tracking.NutritionFact.Extensions;

public static class StartupExtensions
{
	public static IServiceCollection AddNutritionFactService(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		services.AddOptions<NutritionFactSettings>()
			.BindConfiguration(NutritionFactSettings.ConfigurationSectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.AddScoped<INutritionFactService, NutritionFactService>();

		// Cross-module wiring: bind IRecipeService to either local impl (already registered by
		// AddRecipeModule when Recipe is local) or the HTTP RecipeClient (when Recipe is remote).
		// Decision is based on whether the Modules.Recipe feature flag is enabled in this deployment.
		const string recipeFlagPath = "FeatureManagement:Modules.Recipe";
		var recipeIsLocal = configuration.GetValue<bool>(recipeFlagPath);
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
