using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Clients;

/// <summary>
/// Settings for outbound calls to external supplier webhook endpoints.
/// One entry per <c>SupplierKey</c>; the dispatcher worker resolves the URL and shared secret
/// for each request based on the <c>SupplierKey</c> on the incoming event.
/// </summary>
public sealed class SupplierWebhookClientSettings
{
	// Section name is the parent of the dict (`Clients`), not the dict itself. Binding to this section
	// resolves the `Suppliers` property by name and populates it from the section's `Suppliers` child.
	// The previous value `...:Clients:Suppliers` would have required a doubly-nested
	// `Suppliers:Suppliers:<key>` config shape that production didn't have, leaving the dict empty
	// and every supplier dispatch failing with "supplier not configured" at runtime.
	public const string ConfigurationSectionName = "Modules:Sourcing:Procurement:IngredientSupply:Clients";

	/// <summary>
	/// Map of supplier key (e.g. <c>"flour-provider"</c>) → endpoint config.
	/// </summary>
	[Required]
	public IDictionary<string, SupplierEndpoint> Suppliers { get; set; } = new Dictionary<string, SupplierEndpoint>();

	[Range(1, 600)]
	public int TimeoutSeconds { get; set; } = 30;
}

public sealed class SupplierEndpoint
{
	[Required]
	public string Url { get; set; } = string.Empty;

	/// <summary>Shared secret sent in <c>X-Webhook-Secret</c> header (matches what the supplier expects).</summary>
	public string? Secret { get; set; }
}
