using System;
using BurcinCo.BurcinApp.Modules.Recipe.Catalog.RecipePhoto.Api;
using BurcinCo.BurcinApp.Modules.Recipe.Catalog.RecipePhoto.Configuration;
using BurcinCo.BurcinApp.Modules.Recipe.Catalog.RecipePhoto.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.RecipePhoto.Extensions;

public static class StartupExtensions
{
	public static IServiceCollection AddRecipePhotoService(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		services.AddOptions<RecipePhotoSettings>()
			.BindConfiguration(RecipePhotoSettings.ConfigurationSectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Singleton: the service is stateless (HMAC computation only) and lifetime-agnostic.
		services.AddSingleton<IRecipePhotoService, RecipePhotoService>();

		return services;
	}

	public static IEndpointRouteBuilder MapRecipePhotoApi(this IEndpointRouteBuilder endpoints)
	{
		ArgumentNullException.ThrowIfNull(endpoints);
		return RecipePhotoApi.Map(endpoints);
	}
}
