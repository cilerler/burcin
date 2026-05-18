using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using BurcinCo.BurcinApp.Data;
using BurcinCo.BurcinApp.Modules.Recipe.Catalog.Category.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CategoryCodeEntity = BurcinCo.BurcinApp.Models.BurcinDatabase.CategoryCode;
using CategoryGroupEntity = BurcinCo.BurcinApp.Models.BurcinDatabase.CategoryGroup;
using CategoryCodeGroupMappingEntity = BurcinCo.BurcinApp.Models.BurcinDatabase.CategoryCodeGroupMapping;

namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.Category;

internal sealed class CategoryService : ICategoryService
{
	private static readonly ActivitySource _activitySource = new(Constants.Activities.ActivitySourceName);

	private readonly BurcinDatabaseDbContext _db;
	private readonly ILogger<CategoryService> _logger;

	private readonly Counter<long> _codeCreated;
	private readonly Counter<long> _codeUpdated;
	private readonly Counter<long> _codeDeleted;
	private readonly Counter<long> _groupCreated;
	private readonly Counter<long> _groupUpdated;
	private readonly Counter<long> _groupDeleted;
	private readonly Counter<long> _mappingCreated;
	private readonly Counter<long> _mappingDeleted;

	public CategoryService(
		BurcinDatabaseDbContext db,
		IMeterFactory meterFactory,
		ILogger<CategoryService> logger)
	{
		ArgumentNullException.ThrowIfNull(db);
		ArgumentNullException.ThrowIfNull(meterFactory);
		ArgumentNullException.ThrowIfNull(logger);
		_db = db;
		_logger = logger;

		var meter = meterFactory.Create(Constants.Metrics.MeterName);
		_codeCreated = meter.CreateCounter<long>(Constants.Metrics.CodeCreated, unit: "{code}");
		_codeUpdated = meter.CreateCounter<long>(Constants.Metrics.CodeUpdated, unit: "{code}");
		_codeDeleted = meter.CreateCounter<long>(Constants.Metrics.CodeDeleted, unit: "{code}");
		_groupCreated = meter.CreateCounter<long>(Constants.Metrics.GroupCreated, unit: "{group}");
		_groupUpdated = meter.CreateCounter<long>(Constants.Metrics.GroupUpdated, unit: "{group}");
		_groupDeleted = meter.CreateCounter<long>(Constants.Metrics.GroupDeleted, unit: "{group}");
		_mappingCreated = meter.CreateCounter<long>(Constants.Metrics.MappingCreated, unit: "{mapping}");
		_mappingDeleted = meter.CreateCounter<long>(Constants.Metrics.MappingDeleted, unit: "{mapping}");
	}

	// --- CategoryCode ---

	public async Task<IReadOnlyList<CategoryCodeEntity>> GetAllCodesAsync(CancellationToken cancellationToken = default)
	{
		using var activity = _activitySource.StartActivity(nameof(GetAllCodesAsync));
		return await _db.CategoryCodes.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
	}

	public async Task<CategoryCodeEntity?> GetCodeByIdAsync(long id, CancellationToken cancellationToken = default)
	{
		using var activity = _activitySource.StartActivity(nameof(GetCodeByIdAsync));
		activity?.SetTag(Constants.Tags.CategoryCodeId, id);
		return await _db.CategoryCodes.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, cancellationToken).ConfigureAwait(false);
	}

	public async Task<CategoryCodeEntity> CreateCodeAsync(CategoryCodeEntity code, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(code);
		using var activity = _activitySource.StartActivity(nameof(CreateCodeAsync));
		_db.CategoryCodes.Add(code);
		await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		activity?.SetTag(Constants.Tags.CategoryCodeId, code.Id);
		_codeCreated.Add(1, new KeyValuePair<string, object?>(Constants.Tags.CategoryCodeId, code.Id));
		return code;
	}

	public async Task<CategoryCodeEntity?> UpdateCodeAsync(long id, CategoryCodeEntity delta, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(delta);
		using var activity = _activitySource.StartActivity(nameof(UpdateCodeAsync));
		activity?.SetTag(Constants.Tags.CategoryCodeId, id);
		var entity = await _db.CategoryCodes.SingleOrDefaultAsync(c => c.Id == id, cancellationToken).ConfigureAwait(false);
		if (entity is null) return null;
		entity.Code = delta.Code;
		entity.Name = delta.Name;
		await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		_codeUpdated.Add(1, new KeyValuePair<string, object?>(Constants.Tags.CategoryCodeId, id));
		return entity;
	}

	public async Task<bool> DeleteCodeAsync(long id, CancellationToken cancellationToken = default)
	{
		using var activity = _activitySource.StartActivity(nameof(DeleteCodeAsync));
		activity?.SetTag(Constants.Tags.CategoryCodeId, id);
		var entity = await _db.CategoryCodes.SingleOrDefaultAsync(c => c.Id == id, cancellationToken).ConfigureAwait(false);
		if (entity is null) return false;
		_db.CategoryCodes.Remove(entity);
		await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		_codeDeleted.Add(1, new KeyValuePair<string, object?>(Constants.Tags.CategoryCodeId, id));
		return true;
	}

	// --- CategoryGroup ---

	public async Task<IReadOnlyList<CategoryGroupEntity>> GetAllGroupsAsync(CancellationToken cancellationToken = default)
	{
		using var activity = _activitySource.StartActivity(nameof(GetAllGroupsAsync));
		return await _db.CategoryGroups.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
	}

	public async Task<CategoryGroupEntity?> GetGroupByIdAsync(long id, CancellationToken cancellationToken = default)
	{
		using var activity = _activitySource.StartActivity(nameof(GetGroupByIdAsync));
		activity?.SetTag(Constants.Tags.CategoryGroupId, id);
		return await _db.CategoryGroups.AsNoTracking().SingleOrDefaultAsync(g => g.Id == id, cancellationToken).ConfigureAwait(false);
	}

	public async Task<CategoryGroupEntity> CreateGroupAsync(CategoryGroupEntity group, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(group);
		using var activity = _activitySource.StartActivity(nameof(CreateGroupAsync));
		_db.CategoryGroups.Add(group);
		await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		activity?.SetTag(Constants.Tags.CategoryGroupId, group.Id);
		_groupCreated.Add(1, new KeyValuePair<string, object?>(Constants.Tags.CategoryGroupId, group.Id));
		return group;
	}

	public async Task<CategoryGroupEntity?> UpdateGroupAsync(long id, CategoryGroupEntity delta, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(delta);
		using var activity = _activitySource.StartActivity(nameof(UpdateGroupAsync));
		activity?.SetTag(Constants.Tags.CategoryGroupId, id);
		var entity = await _db.CategoryGroups.SingleOrDefaultAsync(g => g.Id == id, cancellationToken).ConfigureAwait(false);
		if (entity is null) return null;
		entity.Name = delta.Name;
		await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		_groupUpdated.Add(1, new KeyValuePair<string, object?>(Constants.Tags.CategoryGroupId, id));
		return entity;
	}

	public async Task<bool> DeleteGroupAsync(long id, CancellationToken cancellationToken = default)
	{
		using var activity = _activitySource.StartActivity(nameof(DeleteGroupAsync));
		activity?.SetTag(Constants.Tags.CategoryGroupId, id);
		var entity = await _db.CategoryGroups.SingleOrDefaultAsync(g => g.Id == id, cancellationToken).ConfigureAwait(false);
		if (entity is null) return false;
		_db.CategoryGroups.Remove(entity);
		await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		_groupDeleted.Add(1, new KeyValuePair<string, object?>(Constants.Tags.CategoryGroupId, id));
		return true;
	}

	// --- CategoryCodeGroupMapping (composite PK) ---

	public async Task<IReadOnlyList<CategoryCodeGroupMappingEntity>> GetAllMappingsAsync(CancellationToken cancellationToken = default)
	{
		using var activity = _activitySource.StartActivity(nameof(GetAllMappingsAsync));
		return await _db.CategoryCodeGroupMappings.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
	}

	public async Task<CategoryCodeGroupMappingEntity?> GetMappingAsync(long categoryCodeId, long categoryGroupId, CancellationToken cancellationToken = default)
	{
		using var activity = _activitySource.StartActivity(nameof(GetMappingAsync));
		activity?.SetTag(Constants.Tags.CategoryCodeId, categoryCodeId);
		activity?.SetTag(Constants.Tags.CategoryGroupId, categoryGroupId);
		return await _db.CategoryCodeGroupMappings
			.AsNoTracking()
			.SingleOrDefaultAsync(m => m.CategoryCodeId == categoryCodeId && m.CategoryGroupId == categoryGroupId, cancellationToken)
			.ConfigureAwait(false);
	}

	public async Task<CategoryCodeGroupMappingEntity> CreateMappingAsync(CategoryCodeGroupMappingEntity mapping, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(mapping);
		using var activity = _activitySource.StartActivity(nameof(CreateMappingAsync));
		activity?.SetTag(Constants.Tags.CategoryCodeId, mapping.CategoryCodeId);
		activity?.SetTag(Constants.Tags.CategoryGroupId, mapping.CategoryGroupId);
		_db.CategoryCodeGroupMappings.Add(mapping);
		await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		_mappingCreated.Add(1,
			new KeyValuePair<string, object?>(Constants.Tags.CategoryCodeId, mapping.CategoryCodeId),
			new KeyValuePair<string, object?>(Constants.Tags.CategoryGroupId, mapping.CategoryGroupId));
		return mapping;
	}

	public async Task<bool> DeleteMappingAsync(long categoryCodeId, long categoryGroupId, CancellationToken cancellationToken = default)
	{
		using var activity = _activitySource.StartActivity(nameof(DeleteMappingAsync));
		activity?.SetTag(Constants.Tags.CategoryCodeId, categoryCodeId);
		activity?.SetTag(Constants.Tags.CategoryGroupId, categoryGroupId);
		var entity = await _db.CategoryCodeGroupMappings
			.SingleOrDefaultAsync(m => m.CategoryCodeId == categoryCodeId && m.CategoryGroupId == categoryGroupId, cancellationToken)
			.ConfigureAwait(false);
		if (entity is null) return false;
		_db.CategoryCodeGroupMappings.Remove(entity);
		await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		_mappingDeleted.Add(1,
			new KeyValuePair<string, object?>(Constants.Tags.CategoryCodeId, categoryCodeId),
			new KeyValuePair<string, object?>(Constants.Tags.CategoryGroupId, categoryGroupId));
		return true;
	}
}
