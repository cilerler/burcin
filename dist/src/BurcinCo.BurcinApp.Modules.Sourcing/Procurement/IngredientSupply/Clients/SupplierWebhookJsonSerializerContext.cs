using System.Text.Json.Serialization;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Abstractions.Events;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Clients;

/// <summary>Source-generated metadata owned by the external supplier HTTP adapter.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(IngredientQuoteRequestedEvent))]
internal partial class SupplierWebhookJsonSerializerContext : JsonSerializerContext
{
}
