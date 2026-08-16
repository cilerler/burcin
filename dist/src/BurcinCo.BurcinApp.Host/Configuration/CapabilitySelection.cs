#if (Sample)
using Microsoft.Extensions.Configuration;
#endif

namespace BurcinCo.BurcinApp.Host.Configuration;

/// <summary>
/// Immutable composition snapshot captured before the service provider is built.
/// Registration and endpoint mapping consume this same instance so configuration reloads
/// cannot produce different application graphs within one process.
/// </summary>
public sealed class CapabilitySelection
{
	public const string ConfigurationSectionName = "FeatureManagement";

#if (Sample)
	// Preserve the template's established deployment-overlay keys while exposing typed properties.
	[ConfigurationKeyName("Modules.Recipe")]
	public bool Recipe { get; init; }

	[ConfigurationKeyName("Modules.Nutrition")]
	public bool Nutrition { get; init; }

	[ConfigurationKeyName("Modules.Sourcing")]
	public bool Sourcing { get; init; }
#endif
}
