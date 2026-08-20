using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Clients;

/// <summary>
/// Settings for outbound calls to external supplier webhook endpoints.
/// One entry per <c>SupplierKey</c>; the quote-request subscriber resolves the URL and shared secret
/// for each request based on the <c>SupplierKey</c> on the incoming event.
/// </summary>
public sealed class SupplierWebhookClientSettings : IValidatableObject
{
	// Section name is the parent of the dict (`Clients`), not the dict itself. Binding to this section
	// resolves the `Suppliers` property by name and populates it from the section's `Suppliers` child.
	// The previous value `...:Clients:Suppliers` would have required a doubly-nested
	// `Suppliers:Suppliers:<key>` config shape that production didn't have, leaving the dict empty
	// and every supplier dispatch failing with "supplier not configured" at runtime.
	public const string ConfigurationSectionName =
		$"{nameof(BurcinCo.BurcinApp.Modules)}:{nameof(BurcinCo.BurcinApp.Modules.Sourcing)}:{nameof(BurcinCo.BurcinApp.Modules.Sourcing.Procurement)}:{nameof(BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply)}:{nameof(BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Clients)}";

	/// <summary>
	/// Map of supplier key (e.g. <c>"flour-provider"</c>) → endpoint config.
	/// </summary>
	[Required]
	[MinLength(1)]
	public IDictionary<string, SupplierEndpoint> Suppliers { get; set; } = new Dictionary<string, SupplierEndpoint>();

	public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(30);

	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		if (Suppliers is null || Suppliers.Count == 0)
		{
			yield return new ValidationResult(
				"At least one supplier endpoint is required.",
				[nameof(Suppliers)]);
			yield break;
		}

		foreach (var (supplierKey, endpoint) in Suppliers)
		{
			if (string.IsNullOrWhiteSpace(supplierKey))
			{
				yield return new ValidationResult(
					"Supplier keys must not be blank.",
					[nameof(Suppliers)]);
				continue;
			}
			if (endpoint is null || string.IsNullOrWhiteSpace(endpoint.Url))
			{
				yield return new ValidationResult(
					$"Supplier '{supplierKey}' must define a URL.",
					[nameof(Suppliers)]);
				continue;
			}
			if (!Uri.TryCreate(endpoint.Url, UriKind.Absolute, out var uri) ||
				(uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
			{
				yield return new ValidationResult(
					$"Supplier '{supplierKey}' must define an absolute HTTP or HTTPS URL.",
					[nameof(Suppliers)]);
			}
		}

		if (HttpTimeout <= TimeSpan.Zero)
		{
			yield return new ValidationResult(
				"HttpTimeout must be positive.",
				[nameof(HttpTimeout)]);
		}
	}
}

public sealed class SupplierEndpoint
{
	[Required]
	public string Url { get; set; } = null!;

	/// <summary>Shared secret sent in <c>X-Webhook-Secret</c> header (matches what the supplier expects).</summary>
	public string? Secret { get; set; }
}
