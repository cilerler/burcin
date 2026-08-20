using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Events;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Abstractions.Events;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Configuration;

/// <summary>
/// Configuration for the IngredientSupply service's message-queue subscriptions.
/// </summary>
public sealed class IngredientSupplySettings : IValidatableObject
{
	public const string ConfigurationSectionName =
		$"{nameof(BurcinCo.BurcinApp.Modules)}:{nameof(BurcinCo.BurcinApp.Modules.Sourcing)}:{nameof(BurcinCo.BurcinApp.Modules.Sourcing.Procurement)}:{nameof(BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply)}";

	/// <summary>
	/// MessageQueue provider name for both the Outbox dispatcher target and the inbox subscriber.
	/// </summary>
	[Required]
	public string MessageQueueProviderName { get; set; } = null!;

	/// <summary>Topic carrying <see cref="IngredientQuoteRequestedEvent"/> messages dispatched from the Outbox.</summary>
	[Required]
	public string IngredientQuoteRequestedEventTopicName { get; set; } = null!;

	/// <summary>Topic carrying <see cref="IngredientQuoteResponseReceivedEvent"/> messages published by the Gateway Webhook adapter.</summary>
	[Required]
	public string IngredientQuoteResponseReceivedEventTopicName { get; set; } = null!;

	[Range(1, 100)]
	public int MaximumDeliveryCount { get; set; } = 2;

	public TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromSeconds(1);

	public TimeSpan MaximumRetryDelay { get; set; } = TimeSpan.FromSeconds(4);

	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		if (string.IsNullOrWhiteSpace(MessageQueueProviderName))
		{
			yield return new ValidationResult(
				"MessageQueueProviderName is required.",
				[nameof(MessageQueueProviderName)]);
		}
		if (string.IsNullOrWhiteSpace(IngredientQuoteRequestedEventTopicName))
		{
			yield return new ValidationResult(
				"IngredientQuoteRequestedEventTopicName is required.",
				[nameof(IngredientQuoteRequestedEventTopicName)]);
		}
		if (string.IsNullOrWhiteSpace(IngredientQuoteResponseReceivedEventTopicName))
		{
			yield return new ValidationResult(
				"IngredientQuoteResponseReceivedEventTopicName is required.",
				[nameof(IngredientQuoteResponseReceivedEventTopicName)]);
		}
		if (InitialRetryDelay <= TimeSpan.Zero)
		{
			yield return new ValidationResult(
				"InitialRetryDelay must be positive.",
				[nameof(InitialRetryDelay)]);
		}
		if (MaximumRetryDelay < InitialRetryDelay)
		{
			yield return new ValidationResult(
				"MaximumRetryDelay must be greater than or equal to InitialRetryDelay.",
				[nameof(MaximumRetryDelay)]);
		}
	}
}
