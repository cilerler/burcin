namespace BurcinCo.BurcinApp.Modules.Nutrition;

/// <summary>
/// Module-wide constants for the Nutrition module.
/// </summary>
internal static class Constants
{
	public const string ModuleName = "Nutrition";

	/// <summary>
	/// Feature-flag key checked by Host startup to gate this module's activation per-deployment.
	/// </summary>
	public static readonly string FeatureFlag = $"Modules.{ModuleName}";

	public const string ConfigurationSectionName = "Modules:Nutrition";
}
