using System.Threading;
using System.Threading.Tasks;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Events;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Requests;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Responses;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Contracts;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Abstractions.Events;
using SourcingIngredientSupplyService = BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.IngredientSupplyService;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Integration.Tests.Fixtures;

/// <summary>Test decorator that records whether an Inbox duplicate reaches the business handler.</summary>
internal sealed class ResponseInvocationTrackingIngredientSupplyService(
	SourcingIngredientSupplyService inner,
	ResponseInvocationState state) : IIngredientSupply
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

	public Task ProcessAsync(
		IngredientQuoteResponseReceivedEvent message,
		CancellationToken cancellationToken)
	{
		state.RecordInvocation();
		return inner.ProcessAsync(message, cancellationToken);
	}
}

internal sealed class ResponseInvocationState
{
	private int _invocationCount;

	public int InvocationCount => Volatile.Read(ref _invocationCount);

	public void RecordInvocation() => Interlocked.Increment(ref _invocationCount);
}
