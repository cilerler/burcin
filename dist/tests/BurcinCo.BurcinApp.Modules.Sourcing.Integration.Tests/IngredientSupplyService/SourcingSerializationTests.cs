using System;
using System.Text.Json;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Events;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Models;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Requests;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Responses;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Serialization;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Abstractions.Events;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Abstractions.Serialization;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Integration.Tests.IngredientSupplyService;

[TestClass]
public sealed class SourcingSerializationTests
{
	[TestMethod]
	public void PublicContracts_SourceGeneratedContexts_RoundTripStableCamelCaseFields()
	{
		var requestedAt = new DateTimeOffset(2026, 8, 10, 12, 30, 0, TimeSpan.Zero);
		var request = new RequestQuoteRequest(
			"test-supplier",
			42,
			[new IngredientLine("flour", 100f, "g")]);
		var response = new IngredientQuoteView(
			7,
			42,
			"test-supplier",
			"Pending",
			requestedAt,
			null,
			null,
			null,
			null);
		var inboundEvent = new IngredientQuoteResponseReceivedEvent(
			7,
			"test-supplier",
			true,
			"{\"ok\":true}",
			null);

		var requestJson = JsonSerializer.Serialize(
			request,
			SourcingJsonSerializerContext.Default.RequestQuoteRequest);
		var responseJson = JsonSerializer.Serialize(
			response,
			IngredientSupplyJsonSerializerContext.Default.IngredientQuoteView);
		var inboundJson = JsonSerializer.Serialize(
			inboundEvent,
			SourcingJsonSerializerContext.Default.IngredientQuoteResponseReceivedEvent);

		StringAssert.Contains(requestJson, "\"supplierKey\":\"test-supplier\"");
		StringAssert.Contains(requestJson, "\"ingredients\"");
		StringAssert.Contains(responseJson, "\"requestedAt\":\"2026-08-10T12:30:00+00:00\"");
		StringAssert.Contains(inboundJson, "\"accepted\":true");
		Assert.IsFalse(requestJson.Contains("SupplierKey", StringComparison.Ordinal));
		Assert.IsFalse(responseJson.Contains("RequestedAt", StringComparison.Ordinal));

		var roundTrip = JsonSerializer.Deserialize(
			requestJson,
			SourcingJsonSerializerContext.Default.RequestQuoteRequest);
		Assert.IsNotNull(roundTrip);
		Assert.AreEqual("test-supplier", roundTrip.SupplierKey);
		Assert.AreEqual(1, roundTrip.Ingredients.Count);
	}

	[TestMethod]
	public void ServiceOwnedOutboxEvent_SourceGeneratedContext_PreservesDateTimeOffsetAndFieldNames()
	{
		var requestedAt = new DateTimeOffset(2026, 8, 10, 12, 30, 0, TimeSpan.FromHours(-4));
		var message = new IngredientQuoteRequestedEvent(
			7,
			null,
			"test-supplier",
			[new IngredientLine("flour", 100f, "g")],
			requestedAt);

		var json = JsonSerializer.Serialize(
			message,
			IngredientSupplyContractJsonSerializerContext.Default.IngredientQuoteRequestedEvent);

		StringAssert.Contains(json, "\"quoteId\":7");
		StringAssert.Contains(json, "\"requestedAt\":\"2026-08-10T12:30:00-04:00\"");
		Assert.IsFalse(json.Contains("QuoteId", StringComparison.Ordinal));

		var roundTrip = JsonSerializer.Deserialize(
			json,
			IngredientSupplyContractJsonSerializerContext.Default.IngredientQuoteRequestedEvent);
		Assert.IsNotNull(roundTrip);
		Assert.AreEqual(requestedAt, roundTrip.RequestedAt);
	}
}
