using System;
using BurcinCo.BurcinApp.Modules.Recipe.Catalog.Category.Configuration;
using BurcinCo.BurcinApp.Modules.Recipe.Catalog.Category.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.Category.Extensions;

public static class StartupExtensions
{
	public static IServiceCollection AddCategoryService(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.AddOptions<CategorySettings>()
			.BindConfiguration(CategorySettings.ConfigurationSectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.AddScoped<ICategoryService, CategoryService>();

		return services;
	}
}
