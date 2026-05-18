using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BurcinCo.BurcinApp.Data;
using BurcinCo.BurcinApp.Modules.Recipe.Abstractions.Interfaces;
using BurcinCo.BurcinApp.Modules.Recipe.Abstractions.Requests;
using BurcinCo.BurcinApp.Modules.Recipe.Abstractions.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RecipeEntity = BurcinCo.BurcinApp.Models.BurcinDatabase.Recipe;

namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.Recipe;

/// <summary>
/// In-process implementation of <see cref="IRecipeService"/>.
/// Reads/writes <see cref="RecipeEntity"/> through the shared <see cref="BurcinDatabaseDbContext"/>.
/// Cross-module callers receive an <see cref="RecipeView"/> projection — never the entity type itself.
/// </summary>
internal sealed partial class RecipeService : IRecipeService
{
	private static readonly ActivitySource _activitySource = new(Constants.Activities.ActivitySourceName);

	private readonly BurcinDatabaseDbContext _db;
	private readonly ILogger<RecipeService> _logger;

	private readonly Counter<long> _created;
	private readonly Counter<long> _updated;
	private readonly Counter<long> _deleted;

	public RecipeService(
		BurcinDatabaseDbContext db,
		IMeterFactory meterFactory,
		ILogger<RecipeService> logger)
	{
		ArgumentNullException.ThrowIfNull(db);
		ArgumentNullException.ThrowIfNull(meterFactory);
		ArgumentNullException.ThrowIfNull(logger);
		_db = db;
		_logger = logger;

		var meter = meterFactory.Create(Constants.Metrics.MeterName);
		_created = meter.CreateCounter<long>(Constants.Metrics.Created, unit: "{recipe}");
		_updated = meter.CreateCounter<long>(Constants.Metrics.Updated, unit: "{recipe}");
		_deleted = meter.CreateCounter<long>(Constants.Metrics.Deleted, unit: "{recipe}");
	}

	public async Task<RecipeView?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
	{
		using var activity = _activitySource.StartActivity(nameof(GetByIdAsync));
		activity?.SetTag(Constants.Tags.RecipeId, id);
		return await _db.Recipes
			.AsNoTracking()
			.Where(r => r.Id == id)
			.Select(r => new RecipeView(r.Id, r.ChefId, r.Name, r.Url, r.Yield, r.GramPerYield, r.CategoryCode))
			.SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
	}

	public async Task<RecipeView> CreateAsync(RecipeCreateRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		using var activity = _activitySource.StartActivity(nameof(CreateAsync));
		activity?.SetTag(Constants.Tags.ChefId, request.ChefId);

		var entity = new RecipeEntity
		{
			ChefId = request.ChefId,
			Name = request.Name,
			Url = request.Url,
			Yield = request.Yield,
			GramPerYield = request.GramPerYield,
			CategoryCode = request.CategoryCode
		};

		_db.Recipes.Add(entity);
		await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		activity?.SetTag(Constants.Tags.RecipeId, entity.Id);
		_created.Add(1,
			new KeyValuePair<string, object?>(Constants.Tags.RecipeId, entity.Id),
			new KeyValuePair<string, object?>(Constants.Tags.ChefId, entity.ChefId));
		LogCreated(entity.Id, entity.ChefId);
		return ToView(entity);
	}

	public async Task<RecipeView?> UpdateAsync(long id, RecipeCreateRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		using var activity = _activitySource.StartActivity(nameof(UpdateAsync));
		activity?.SetTag(Constants.Tags.RecipeId, id);

		var entity = await _db.Recipes.SingleOrDefaultAsync(r => r.Id == id, cancellationToken).ConfigureAwait(false);
		if (entity is null) return null;

		entity.Name = request.Name;
		entity.Url = request.Url;
		entity.Yield = request.Yield;
		entity.GramPerYield = request.GramPerYield;
		entity.CategoryCode = request.CategoryCode;
		await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		_updated.Add(1, new KeyValuePair<string, object?>(Constants.Tags.RecipeId, entity.Id));
		LogUpdated(entity.Id);
		return ToView(entity);
	}

	public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
	{
		using var activity = _activitySource.StartActivity(nameof(DeleteAsync));
		activity?.SetTag(Constants.Tags.RecipeId, id);

		var entity = await _db.Recipes.SingleOrDefaultAsync(r => r.Id == id, cancellationToken).ConfigureAwait(false);
		if (entity is null) return false;

		_db.Recipes.Remove(entity);
		await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		_deleted.Add(1, new KeyValuePair<string, object?>(Constants.Tags.RecipeId, id));
		LogDeleted(id);
		return true;
	}

	private static RecipeView ToView(RecipeEntity entity) =>
		new(entity.Id, entity.ChefId, entity.Name, entity.Url, entity.Yield, entity.GramPerYield, entity.CategoryCode);

	[LoggerMessage(EventId = 3001, Level = LogLevel.Information, Message = "Recipe created. Id={RecipeId} ChefId={ChefId}")]
	private partial void LogCreated(long recipeId, long chefId);

	[LoggerMessage(EventId = 3002, Level = LogLevel.Information, Message = "Recipe updated. Id={RecipeId}")]
	private partial void LogUpdated(long recipeId);

	[LoggerMessage(EventId = 3003, Level = LogLevel.Information, Message = "Recipe deleted. Id={RecipeId}")]
	private partial void LogDeleted(long recipeId);
}
