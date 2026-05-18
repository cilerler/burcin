using System;
using BurcinCo.BurcinApp.Modules.Nutrition.Tracking.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BurcinCo.BurcinApp.Modules.Nutrition.Extensions;

/// <summary>
/// Module-level wiring for Nutrition. Activation in a given deployment is gated by the
/// <c>Modules.Nutrition</c> feature flag — Host only calls <see cref="AddNutritionModule"/>
/// when that flag is enabled.
///
/// HTTP endpoints are exposed via <c>NutritionFactController</c> (an <c>ODataController</c>),
/// auto-discovered by <c>app.MapControllers()</c>. EDM contribution lives in
/// <see cref="ODataExtensions.AddNutritionEntitySets"/>.
/// </summary>
public static class StartupExtensions
{
	public static IServiceCollection AddNutritionModule(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		services.AddTrackingComponent(configuration);

		return services;
	}
}
