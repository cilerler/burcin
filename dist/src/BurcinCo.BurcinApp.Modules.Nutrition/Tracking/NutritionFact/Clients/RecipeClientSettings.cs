using System.ComponentModel.DataAnnotations;

namespace BurcinCo.BurcinApp.Modules.Nutrition.Tracking.NutritionFact.Clients;

/// <summary>
/// Settings for the RecipeClient HTTP wrapper. Read when Recipe runs as a separate
/// k8s Deployment (i.e. the <c>Modules.Recipe</c> feature flag is OFF in this deployment).
/// </summary>
public sealed class RecipeClientSettings
{
	public const string ConfigurationSectionName =
		$"{nameof(BurcinCo.BurcinApp.Modules)}:{nameof(BurcinCo.BurcinApp.Modules.Nutrition)}:{nameof(BurcinCo.BurcinApp.Modules.Nutrition.Tracking)}:{nameof(BurcinCo.BurcinApp.Modules.Nutrition.Tracking.NutritionFact)}:{nameof(BurcinCo.BurcinApp.Modules.Nutrition.Tracking.NutritionFact.Clients)}:{nameof(BurcinCo.BurcinApp.Modules.Recipe)}";

	/// <summary>
	/// Base URL of the Recipe module's deployment. Example: <c>http://recipe.svc.cluster.local</c> or
	/// <c>https://recipe.internal:443</c>. Required when Recipe is remote.
	/// </summary>
	[Required]
	public string BaseAddress { get; set; } = string.Empty;

	[Range(1, 600)]
	public int TimeoutSeconds { get; set; } = 30;
}
