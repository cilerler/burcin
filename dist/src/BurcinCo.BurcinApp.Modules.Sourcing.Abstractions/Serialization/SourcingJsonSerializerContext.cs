using System.Collections.Generic;
using System.Text.Json.Serialization;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Events;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Models;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Requests;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Responses;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Serialization;

/// <summary>
/// Source-generated serializer metadata owned by the Sourcing module's public contract boundary.
/// Consumers use this context instead of reflection-based serialization.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(IngredientLine))]
[JsonSerializable(typeof(IReadOnlyList<IngredientLine>))]
[JsonSerializable(typeof(List<IngredientLine>))]
[JsonSerializable(typeof(RequestQuoteRequest))]
[JsonSerializable(typeof(IngredientQuoteView))]
[JsonSerializable(typeof(IngredientQuoteResponseReceivedEvent))]
public partial class SourcingJsonSerializerContext : JsonSerializerContext
{
}
