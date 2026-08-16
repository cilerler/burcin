using System;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Serialization;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Abstractions.Serialization;
using Ruya.Services.MessageQueue.Extensions;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Extensions;

/// <summary>
/// Registers the producer-owned JSON metadata for every broker contract published or consumed by
/// the Sourcing module.
/// </summary>
public static class MessageQueueBuilderExtensions
{
	/// <summary>
	/// Adds Sourcing's source-generated broker contract metadata to the selected queue serializer.
	/// </summary>
	public static IMessageQueueBuilder AddSourcingMessageContracts(this IMessageQueueBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		return builder
			.AddJsonSerializerContext(IngredientSupplyContractJsonSerializerContext.Default)
			.AddJsonSerializerContext(SourcingJsonSerializerContext.Default);
	}
}
