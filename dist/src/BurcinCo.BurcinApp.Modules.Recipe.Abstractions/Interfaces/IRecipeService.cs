using System.Threading;
using System.Threading.Tasks;
using BurcinCo.BurcinApp.Modules.Recipe.Abstractions.Requests;
using BurcinCo.BurcinApp.Modules.Recipe.Abstractions.Responses;

namespace BurcinCo.BurcinApp.Modules.Recipe.Abstractions.Interfaces;

/// <summary>
/// Public command/query surface for the Recipe service.
/// Cross-module callers depend on this interface; sibling-module HTTP clients
/// (e.g. Modules.Nutrition.Tracking.NutritionFact.Clients.RecipeClient) implement it
/// to invoke a remote Recipe-module deployment when modules run as separate k8s Deployments.
/// The in-process implementation is registered by AddRecipeModule().
/// </summary>
public interface IRecipeService
{
	Task<RecipeView?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

	Task<RecipeView> CreateAsync(RecipeCreateRequest request, CancellationToken cancellationToken = default);

	Task<RecipeView?> UpdateAsync(long id, RecipeCreateRequest request, CancellationToken cancellationToken = default);

	Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);
}
