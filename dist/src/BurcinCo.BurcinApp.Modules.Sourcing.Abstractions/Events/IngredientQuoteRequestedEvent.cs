using System;
using System.Collections.Generic;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Models;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Events;

/// <summary>
/// Outbound event written to the Outbox when a quote is requested. The
/// MessageQueueOutboundDispatcher publishes this to the broker; the QuoteRequestDispatcher
/// worker consumes from the broker and makes the actual HTTP call to the external supplier.
/// </summary>
public record IngredientQuoteRequestedEvent(
	long QuoteId,
	long? RecipeId,
	string SupplierKey,
	IReadOnlyList<IngredientLine> Ingredients,
	DateTime RequestedAt);
