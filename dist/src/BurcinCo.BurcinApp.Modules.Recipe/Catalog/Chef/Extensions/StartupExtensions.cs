using System;
using BurcinCo.BurcinApp.Modules.Recipe.Catalog.Chef.Configuration;
using BurcinCo.BurcinApp.Modules.Recipe.Catalog.Chef.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.Chef.Extensions;

public static class StartupExtensions
{
	public static IServiceCollection AddChefService(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		services.AddOptions<ChefSettings>()
			.BindConfiguration(ChefSettings.ConfigurationSectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.AddScoped<IChefService, ChefService>();

		return services;
	}
}
