using System.Threading;
using System.Threading.Tasks;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Events;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Requests;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Responses;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Abstractions.Events;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Contracts;

internal interface IIngredientSupply
{
	Task<IngredientQuoteView> RequestQuoteAsync(RequestQuoteRequest request, CancellationToken cancellationToken);

	Task<IngredientQuoteView?> GetByIdAsync(long quoteId, CancellationToken cancellationToken);

	Task ProcessAsync(IngredientQuoteRequestedEvent message, CancellationToken cancellationToken);

	Task ProcessAsync(IngredientQuoteResponseReceivedEvent message, CancellationToken cancellationToken);
}
