#if (Sample)
using Microsoft.Extensions.Configuration;
#endif

namespace BurcinCo.BurcinApp.Gateway.Configuration;

/// <summary>
/// Immutable Gateway capability graph captured before the service provider is built.
/// Registration and endpoint mapping consume this same instance for the lifetime of the process.
/// </summary>
public sealed class CapabilitySelection
{
	public const string ConfigurationSectionName = "FeatureManagement";

#if (Sample)
	[ConfigurationKeyName("Gateway.Webhook")]
	public bool Webhook { get; init; }
#endif
}
