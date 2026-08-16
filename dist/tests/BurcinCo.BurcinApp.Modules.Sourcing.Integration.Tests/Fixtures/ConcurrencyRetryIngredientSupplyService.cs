using System.Threading;
using System.Threading.Tasks;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Events;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Requests;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Responses;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Contracts;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Abstractions.Events;
using Microsoft.EntityFrameworkCore;
using SourcingIngredientSupplyService = BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.IngredientSupplyService;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Integration.Tests.Fixtures;

/// <summary>Test decorator that forces one retry after the real business mutation has been saved.</summary>
internal sealed class ConcurrencyRetryIngredientSupplyService(
	SourcingIngredientSupplyService inner,
	ConcurrencyRetryState state) : IIngredientSupply
{
	public Task<IngredientQuoteView> RequestQuoteAsync(
		RequestQuoteRequest request,
		CancellationToken cancellationToken) =>
		inner.RequestQuoteAsync(request, cancellationToken);

	public Task<IngredientQuoteView?> GetByIdAsync(
		long quoteId,
		CancellationToken cancellationToken) =>
		inner.GetByIdAsync(quoteId, cancellationToken);

	public Task ProcessAsync(
		IngredientQuoteRequestedEvent message,
		CancellationToken cancellationToken) =>
		inner.ProcessAsync(message, cancellationToken);

	public async Task ProcessAsync(
		IngredientQuoteResponseReceivedEvent message,
		CancellationToken cancellationToken)
	{
		var attempt = state.BeginAttempt();
		var attemptMessage = message with { RawResponseJson = $"{{\"attempt\":{attempt}}}" };
		await inner.ProcessAsync(attemptMessage, cancellationToken).ConfigureAwait(false);
		if (attempt == 1)
		{
			throw new DbUpdateConcurrencyException("Simulated conflict after the first enlisted business save.");
		}
	}
}

internal sealed class ConcurrencyRetryState
{
	private int _attempts;

	public int Attempts => Volatile.Read(ref _attempts);

	public int BeginAttempt() => Interlocked.Increment(ref _attempts);
}
