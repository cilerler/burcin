namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.Recipe.Configuration;

/// <summary>
/// Configuration for the Recipe service.
/// Bound from <see cref="ConfigurationSectionName"/>.
/// </summary>
public sealed class RecipeSettings
{
	public const string ConfigurationSectionName =
		$"{nameof(BurcinCo.BurcinApp.Modules)}:{nameof(BurcinCo.BurcinApp.Modules.Recipe)}:{nameof(BurcinCo.BurcinApp.Modules.Recipe.Catalog)}:{nameof(BurcinCo.BurcinApp.Modules.Recipe.Catalog.Recipe)}";
}
