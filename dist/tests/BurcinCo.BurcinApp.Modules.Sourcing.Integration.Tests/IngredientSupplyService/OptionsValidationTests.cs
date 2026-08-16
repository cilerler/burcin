using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Clients;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Integration.Tests.IngredientSupplyService;

[TestClass]
public sealed class OptionsValidationTests
{
	[TestMethod]
	public void IngredientSupplySettings_BlankRoutingAndInvalidDeliveryCount_FailsValidation()
	{
		var settings = new IngredientSupplySettings
		{
			MessageQueueProviderName = " ",
			IngredientQuoteRequestedEventTopicName = " ",
			IngredientQuoteResponseReceivedEventTopicName = " ",
			MaximumDeliveryCount = 0,
		};

		var errors = Validate(settings);

		Assert.IsTrue(errors.Any(error => error.MemberNames.Contains(nameof(settings.MessageQueueProviderName))));
		Assert.IsTrue(errors.Any(error => error.MemberNames.Contains(nameof(settings.IngredientQuoteRequestedEventTopicName))));
		Assert.IsTrue(errors.Any(error => error.MemberNames.Contains(nameof(settings.IngredientQuoteResponseReceivedEventTopicName))));
		Assert.IsTrue(errors.Any(error => error.MemberNames.Contains(nameof(settings.MaximumDeliveryCount))));
	}

	[TestMethod]
	public void IngredientSupplySettings_MaximumRetryDelayBelowInitial_FailsValidation()
	{
		var settings = new IngredientSupplySettings
		{
			MessageQueueProviderName = "sourcing-rabbitmq",
			IngredientQuoteRequestedEventTopicName = "sourcing.ingredient-quote.requested",
			IngredientQuoteResponseReceivedEventTopicName = "webhooks.sourcing.quote-response",
			InitialRetryDelay = TimeSpan.FromSeconds(5),
			MaximumRetryDelay = TimeSpan.FromSeconds(1),
		};

		var errors = Validate(settings);

		Assert.IsTrue(errors.Any(error => error.MemberNames.Contains(nameof(settings.MaximumRetryDelay))));
	}

	[TestMethod]
	public void SupplierWebhookClientSettings_EmptyOrInvalidSupplierMap_FailsValidation()
	{
		var empty = new SupplierWebhookClientSettings();
		var invalid = new SupplierWebhookClientSettings
		{
			Suppliers = new Dictionary<string, SupplierEndpoint>
			{
				["test-supplier"] = new() { Url = "relative/path" },
			},
			HttpTimeout = TimeSpan.Zero,
		};

		var emptyErrors = Validate(empty);
		var invalidErrors = Validate(invalid);

		Assert.IsTrue(emptyErrors.Any(error => error.MemberNames.Contains(nameof(empty.Suppliers))));
		Assert.IsTrue(invalidErrors.Any(error => error.MemberNames.Contains(nameof(invalid.Suppliers))));
		Assert.IsTrue(invalidErrors.Any(error => error.MemberNames.Contains(nameof(invalid.HttpTimeout))));
	}

	[TestMethod]
	public void SupplierWebhookClientSettings_AbsoluteHttpEndpointAndPositiveTimeout_PassesValidation()
	{
		var settings = new SupplierWebhookClientSettings
		{
			Suppliers = new Dictionary<string, SupplierEndpoint>
			{
				["test-supplier"] = new() { Url = "https://supplier.test/quote" },
			},
			HttpTimeout = TimeSpan.FromSeconds(30),
		};

		Assert.AreEqual(0, Validate(settings).Count);
	}

	private static IReadOnlyList<ValidationResult> Validate(object options)
	{
		var results = new List<ValidationResult>();
		Validator.TryValidateObject(
			options,
			new ValidationContext(options),
			results,
			validateAllProperties: true);
		return results;
	}
}
