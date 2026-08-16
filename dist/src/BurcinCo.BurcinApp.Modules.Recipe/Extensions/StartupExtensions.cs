using System;
using BurcinCo.BurcinApp.Modules.Recipe.Catalog.Extensions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace BurcinCo.BurcinApp.Modules.Recipe.Extensions;

/// <summary>
/// Module-level wiring for Recipe. Registers all components in this module.
/// Activation in a given deployment is gated by the <c>Modules.Recipe</c> feature flag —
/// Host only calls <see cref="AddRecipeModule"/> when that flag is enabled.
///
/// HTTP endpoints split:
///   - OData controllers (Chef, Recipe, Category*, Tag): auto-discovered by <c>app.MapControllers()</c>.
///     EDM contribution lives in <see cref="ODataExtensions.AddRecipeEntitySets"/>.
///   - Minimal-API endpoints (RecipePhoto signed URL + download stub): mapped explicitly via
///     <see cref="MapRecipeModule"/>. Host calls this when the feature flag is on.
/// </summary>
public static class StartupExtensions
{
	public static IServiceCollection AddRecipeModule(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.AddCatalogComponent();

		return services;
	}

	public static IEndpointRouteBuilder MapRecipeModule(
		this IEndpointRouteBuilder endpoints,
		bool enabled)
	{
		ArgumentNullException.ThrowIfNull(endpoints);

		if (!enabled)
		{
			return endpoints;
		}

		endpoints.MapCatalogComponent(enabled);
		return endpoints;
	}
}
