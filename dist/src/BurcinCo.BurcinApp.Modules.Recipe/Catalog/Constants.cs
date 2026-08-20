namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog;

/// <summary>
/// Component-wide constants for the Catalog component within the Recipe module.
/// </summary>
internal static class Constants
{
	public const string ComponentName = nameof(BurcinCo.BurcinApp.Modules.Recipe.Catalog);

	/// <summary>
	/// Configuration section name for component-wide settings.
	/// </summary>
	public const string ConfigurationSectionName =
		$"{nameof(BurcinCo.BurcinApp.Modules)}:{nameof(BurcinCo.BurcinApp.Modules.Recipe)}:{ComponentName}";
}
