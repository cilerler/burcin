using System.Text.Json.Serialization;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Abstractions.Events;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Abstractions.Serialization;

/// <summary>
/// Producer-owned serializer metadata for IngredientSupply broker contracts. It stays with the
/// service abstraction boundary rather than the HTTP adapter's serializer context.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(IngredientQuoteRequestedEvent))]
public partial class IngredientSupplyContractJsonSerializerContext : JsonSerializerContext
{
}
