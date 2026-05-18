using System.ComponentModel.DataAnnotations;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Configuration;

/// <summary>
/// Configuration for the IngredientSupply service. The MessageQueue provider name is
/// shared with the rest of the app (default <c>"default"</c> per <c>MessageQueue:DefaultProvider</c>).
/// </summary>
public sealed class IngredientSupplySettings
{
	public const string ConfigurationSectionName = "Modules:Sourcing:Procurement:IngredientSupply";

	/// <summary>
	/// MessageQueue provider name for both the Outbox dispatcher target and the inbox subscriber.
	/// </summary>
	[Required]
	public string MessageQueueProviderName { get; set; } = "default";
}
