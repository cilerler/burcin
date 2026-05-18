using System;
using BurcinCo.BurcinApp.Modules.Recipe.Abstractions.Interfaces;
using BurcinCo.BurcinApp.Modules.Recipe.Catalog.Recipe.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.Recipe.Extensions;

/// <summary>
/// Service-level DI registration for the Recipe service. HTTP exposure is via
/// <c>RecipeController</c> (OData), which is discovered by <c>app.MapControllers()</c>.
/// </summary>
public static class StartupExtensions
{
	public static IServiceCollection AddRecipeService(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		services.AddOptions<RecipeSettings>()
			.BindConfiguration(RecipeSettings.ConfigurationSectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Cross-module-public IRecipeService → internal RecipeService.
		// The interface is published in Modules.Recipe.Abstractions so sibling-module
		// HTTP clients can implement it when Recipe runs as a separate deployment.
		services.AddScoped<IRecipeService, RecipeService>();

		return services;
	}
}
