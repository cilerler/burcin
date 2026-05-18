using System;
using BurcinCo.BurcinApp.Modules.Recipe.Catalog.Category.Configuration;
using BurcinCo.BurcinApp.Modules.Recipe.Catalog.Category.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.Category.Extensions;

public static class StartupExtensions
{
	public static IServiceCollection AddCategoryService(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		services.AddOptions<CategorySettings>()
			.BindConfiguration(CategorySettings.ConfigurationSectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.AddScoped<ICategoryService, CategoryService>();

		return services;
	}
}
