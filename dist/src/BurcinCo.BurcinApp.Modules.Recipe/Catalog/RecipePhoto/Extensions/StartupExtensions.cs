using System;
using BurcinCo.BurcinApp.Modules.Recipe.Catalog.RecipePhoto.Api;
using BurcinCo.BurcinApp.Modules.Recipe.Catalog.RecipePhoto.Configuration;
using BurcinCo.BurcinApp.Modules.Recipe.Catalog.RecipePhoto.Contracts;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.RecipePhoto.Extensions;

public static class StartupExtensions
{
	public static IServiceCollection AddRecipePhotoService(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.AddOptions<RecipePhotoSettings>()
			.BindConfiguration(RecipePhotoSettings.ConfigurationSectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Singleton: the service is stateless (HMAC computation only) and lifetime-agnostic.
		services.AddSingleton<IRecipePhotoService, RecipePhotoService>();

		return services;
	}

	public static IEndpointRouteBuilder MapRecipePhotoApi(
		this IEndpointRouteBuilder endpoints,
		bool enabled)
	{
		ArgumentNullException.ThrowIfNull(endpoints);

		if (!enabled)
		{
			return endpoints;
		}

		var serviceProviderIsService =
			endpoints.ServiceProvider.GetRequiredService<IServiceProviderIsService>();

		if (!serviceProviderIsService.IsService(typeof(IRecipePhotoService)))
		{
			throw new InvalidOperationException(
				"Cannot map RecipePhoto endpoints because IRecipePhotoService is not registered. " +
				"Run the Recipe registration cascade before endpoint mapping.");
		}

		return RecipePhotoApi.Map(endpoints);
	}
}
