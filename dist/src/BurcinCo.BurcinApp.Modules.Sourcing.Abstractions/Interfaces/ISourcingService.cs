using System.Threading;
using System.Threading.Tasks;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Requests;
using BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Responses;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Abstractions.Interfaces;

/// <summary>
/// Public command/query surface for the Sourcing module. Other modules call this when they
/// need to initiate or read an external supplier quote (e.g., a future Modules.Recipe
/// scheduling feature could trigger a quote when a recipe is queued).
/// </summary>
public interface ISourcingService
{
	Task<IngredientQuoteView> RequestQuoteAsync(RequestQuoteRequest request, CancellationToken cancellationToken);

	Task<IngredientQuoteView?> GetByIdAsync(long quoteId, CancellationToken cancellationToken);
}
