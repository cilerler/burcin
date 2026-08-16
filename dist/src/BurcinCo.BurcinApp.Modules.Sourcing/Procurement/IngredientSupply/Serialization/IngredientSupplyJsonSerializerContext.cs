using System.Text.Json.Serialization;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Requests;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Responses;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Serialization;

/// <summary>Source-generated metadata for the IngredientSupply HTTP adapter.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(RequestQuoteRequest))]
[JsonSerializable(typeof(IngredientQuoteView))]
internal partial class IngredientSupplyJsonSerializerContext : JsonSerializerContext
{
}
