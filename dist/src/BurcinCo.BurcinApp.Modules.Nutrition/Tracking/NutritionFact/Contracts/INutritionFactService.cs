using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NutritionFactEntity = BurcinCo.BurcinApp.Models.Zignec.NutritionFact;

namespace BurcinCo.BurcinApp.Modules.Nutrition.Tracking.NutritionFact.Contracts;

// Public because NutritionFactController (public for MVC discovery) takes this in its constructor.
public interface INutritionFactService
{
	Task<IReadOnlyList<NutritionFactEntity>> GetAllAsync(CancellationToken cancellationToken = default);

	Task<NutritionFactEntity?> GetByRecipeIdAsync(long recipeId, CancellationToken cancellationToken = default);

	/// <summary>
	/// Creates a NutritionFact for the given recipe.
	/// Validates recipe existence by calling <c>IRecipeService</c> (in-process if Recipe runs locally,
	/// otherwise through the HTTP client in Clients/RecipeClient.cs).
	/// Returns null if the referenced recipe doesn't exist.
	/// </summary>
	Task<NutritionFactEntity?> CreateAsync(NutritionFactEntity fact, CancellationToken cancellationToken = default);

	Task<NutritionFactEntity?> UpdateAsync(long recipeId, NutritionFactEntity delta, CancellationToken cancellationToken = default);

	Task<bool> DeleteAsync(long recipeId, CancellationToken cancellationToken = default);
}
