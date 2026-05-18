using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ChefEntity = BurcinCo.BurcinApp.Models.Zignec.Chef;

namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.Chef.Contracts;

/// <summary>
/// Contract for the Chef service. Public because <c>ChefController</c> (an OData controller — must
/// itself be public for MVC discovery) takes this in its constructor, and a public ctor can't
/// reference an internal type. Chef is otherwise an internal aggregate of the Catalog component
/// owned by Modules.Recipe; no sibling module has an in-process IChefService dependency.
/// </summary>
public interface IChefService
{
	Task<IReadOnlyList<ChefEntity>> GetAllAsync(CancellationToken cancellationToken = default);

	Task<ChefEntity?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

	Task<ChefEntity> CreateAsync(ChefEntity chef, CancellationToken cancellationToken = default);

	Task<ChefEntity?> UpdateAsync(long id, ChefEntity delta, CancellationToken cancellationToken = default);

	Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);
}
