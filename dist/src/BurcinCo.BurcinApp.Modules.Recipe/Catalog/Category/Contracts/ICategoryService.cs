using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CategoryCodeEntity = BurcinCo.BurcinApp.Models.BurcinDatabase.CategoryCode;
using CategoryGroupEntity = BurcinCo.BurcinApp.Models.BurcinDatabase.CategoryGroup;
using CategoryCodeGroupMappingEntity = BurcinCo.BurcinApp.Models.BurcinDatabase.CategoryCodeGroupMapping;

namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.Category.Contracts;

/// <summary>
/// Category service contract. Public because the three Category-related OData controllers
/// (CategoryCodeController, CategoryGroupController, CategoryCodeGroupMappingController — public for
/// MVC discovery) take this in their constructors. Combines CategoryCode + CategoryGroup +
/// CategoryCodeGroupMapping CRUD into one service since they form a tightly-coupled M:M relationship
/// that's only meaningful when managed together.
/// </summary>
public interface ICategoryService
{
	// --- CategoryCode ---
	Task<IReadOnlyList<CategoryCodeEntity>> GetAllCodesAsync(CancellationToken cancellationToken = default);
	Task<CategoryCodeEntity?> GetCodeByIdAsync(long id, CancellationToken cancellationToken = default);
	Task<CategoryCodeEntity> CreateCodeAsync(CategoryCodeEntity code, CancellationToken cancellationToken = default);
	Task<CategoryCodeEntity?> UpdateCodeAsync(long id, CategoryCodeEntity delta, CancellationToken cancellationToken = default);
	Task<bool> DeleteCodeAsync(long id, CancellationToken cancellationToken = default);

	// --- CategoryGroup ---
	Task<IReadOnlyList<CategoryGroupEntity>> GetAllGroupsAsync(CancellationToken cancellationToken = default);
	Task<CategoryGroupEntity?> GetGroupByIdAsync(long id, CancellationToken cancellationToken = default);
	Task<CategoryGroupEntity> CreateGroupAsync(CategoryGroupEntity group, CancellationToken cancellationToken = default);
	Task<CategoryGroupEntity?> UpdateGroupAsync(long id, CategoryGroupEntity delta, CancellationToken cancellationToken = default);
	Task<bool> DeleteGroupAsync(long id, CancellationToken cancellationToken = default);

	// --- CategoryCodeGroupMapping (composite PK on (CategoryCodeId, CategoryGroupId)) ---
	Task<IReadOnlyList<CategoryCodeGroupMappingEntity>> GetAllMappingsAsync(CancellationToken cancellationToken = default);
	Task<CategoryCodeGroupMappingEntity?> GetMappingAsync(long categoryCodeId, long categoryGroupId, CancellationToken cancellationToken = default);
	Task<CategoryCodeGroupMappingEntity> CreateMappingAsync(CategoryCodeGroupMappingEntity mapping, CancellationToken cancellationToken = default);
	Task<bool> DeleteMappingAsync(long categoryCodeId, long categoryGroupId, CancellationToken cancellationToken = default);
}
