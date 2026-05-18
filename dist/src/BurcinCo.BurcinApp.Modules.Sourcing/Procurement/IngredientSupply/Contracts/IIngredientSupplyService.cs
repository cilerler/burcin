using System.Threading;
using System.Threading.Tasks;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Requests;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Responses;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.Contracts;

internal interface IIngredientSupplyService
{
	Task<IngredientQuoteView> RequestQuoteAsync(RequestQuoteRequest request, CancellationToken cancellationToken = default);

	Task<IngredientQuoteView?> GetByIdAsync(long quoteId, CancellationToken cancellationToken = default);
}
