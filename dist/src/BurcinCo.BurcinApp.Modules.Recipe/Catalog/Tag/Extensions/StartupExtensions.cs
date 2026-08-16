using System;
using BurcinCo.BurcinApp.Modules.Recipe.Catalog.Tag.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.Tag.Extensions;

public static class StartupExtensions
{
	public static IServiceCollection AddTagService(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Singleton because the in-memory dictionary IS the database. A scoped lifetime would
		// throw away every tag on each request boundary.
		services.AddSingleton<ITagService, TagService>();

		return services;
	}
}
