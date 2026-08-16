using System;
using BurcinCo.BurcinApp.Modules.Recipe.Catalog.Chef.Configuration;
using BurcinCo.BurcinApp.Modules.Recipe.Catalog.Chef.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.Chef.Extensions;

public static class StartupExtensions
{
	public static IServiceCollection AddChefService(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.AddOptions<ChefSettings>()
			.BindConfiguration(ChefSettings.ConfigurationSectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.AddScoped<IChefService, ChefService>();

		return services;
	}
}
