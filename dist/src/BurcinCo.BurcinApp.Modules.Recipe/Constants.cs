namespace BurcinCo.BurcinApp.Modules.Recipe;

/// <summary>
/// Module-wide constants for the Recipe module.
/// </summary>
internal static class Constants
{
	public const string ModuleName = "Recipe";

	/// <summary>
	/// Feature-flag key checked by Host startup. When the flag is off in this deployment,
	/// the module's StartupExtensions are not invoked and none of its components/services run.
	/// </summary>
	public static readonly string FeatureFlag = $"Modules.{ModuleName}";

	/// <summary>
	/// Configuration section name for module-wide settings (rare; most config lives at service level).
	/// </summary>
	public const string ConfigurationSectionName = "Modules:Recipe";
}
