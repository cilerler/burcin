using System;
using BurcinCo.BurcinApp.Modules.Recipe.Catalog.Category.Extensions;
using BurcinCo.BurcinApp.Modules.Recipe.Catalog.Chef.Extensions;
using BurcinCo.BurcinApp.Modules.Recipe.Catalog.Recipe.Extensions;
using BurcinCo.BurcinApp.Modules.Recipe.Catalog.RecipePhoto.Extensions;
using BurcinCo.BurcinApp.Modules.Recipe.Catalog.Tag.Extensions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.Extensions;

/// <summary>
/// Component-level wiring for Catalog. Registers all services in this component.
/// HTTP exposure: most services use <c>ODataController</c> classes (auto-discovered by
/// <c>app.MapControllers()</c> at the Host level). The exception is <c>RecipePhoto</c>, which uses
/// minimal-API endpoints — those need explicit mapping via <see cref="MapCatalogComponent"/>.
/// </summary>
public static class StartupExtensions
{
	public static IServiceCollection AddCatalogComponent(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		services.AddRecipeService(configuration);
		services.AddChefService(configuration);
		services.AddCategoryService(configuration);
		services.AddTagService(configuration);
		services.AddRecipePhotoService(configuration);

		return services;
	}

	public static IEndpointRouteBuilder MapCatalogComponent(this IEndpointRouteBuilder endpoints)
	{
		ArgumentNullException.ThrowIfNull(endpoints);
		endpoints.MapRecipePhotoApi();
		return endpoints;
	}
}
