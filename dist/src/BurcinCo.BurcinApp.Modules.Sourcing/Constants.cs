namespace BurcinCo.BurcinApp.Modules.Sourcing;

/// <summary>
/// Module-wide constants for the Sourcing module — the reference implementation of the
/// outbound producer (Outbox → broker → external HTTP) and inbound consumer
/// (external HTTP → Gateway Webhook adapter → broker → Inbox dedup → handler) flows.
/// </summary>
internal static class Constants
{
	public const string ModuleName = "Sourcing";

	public static readonly string FeatureFlag = $"Modules.{ModuleName}";

	public const string ConfigurationSectionName = "Modules:Sourcing";
}
