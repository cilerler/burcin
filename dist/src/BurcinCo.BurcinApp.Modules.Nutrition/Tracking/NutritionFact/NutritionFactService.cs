using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using BurcinCo.BurcinApp.Data;
using BurcinCo.BurcinApp.Modules.Nutrition.Tracking.NutritionFact.Contracts;
using BurcinCo.BurcinApp.Modules.Recipe.Abstractions.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NutritionFactEntity = BurcinCo.BurcinApp.Models.BurcinDatabase.NutritionFact;

namespace BurcinCo.BurcinApp.Modules.Nutrition.Tracking.NutritionFact;

/// <summary>
/// In-process implementation of <see cref="INutritionFactService"/>.
/// Demonstrates the cross-module-call pattern: validates recipe existence via <see cref="IRecipeService"/>
/// before insert. The IRecipeService binding resolves to either a local Recipe-module impl or to the
/// HTTP client in Clients/RecipeClient.cs depending on whether Recipe runs in this deployment.
/// </summary>
internal sealed partial class NutritionFactService : INutritionFactService
{
	private static readonly ActivitySource _activitySource = new(Constants.Activities.ActivitySourceName);

	private readonly BurcinDatabaseDbContext _db;
	private readonly IRecipeService _recipes;
	private readonly ILogger<NutritionFactService> _logger;

	private readonly Counter<long> _created;
	private readonly Counter<long> _updated;
	private readonly Counter<long> _deleted;

	public NutritionFactService(
		BurcinDatabaseDbContext db,
		IRecipeService recipes,
		IMeterFactory meterFactory,
		ILogger<NutritionFactService> logger)
	{
		ArgumentNullException.ThrowIfNull(db);
		ArgumentNullException.ThrowIfNull(recipes);
		ArgumentNullException.ThrowIfNull(meterFactory);
		ArgumentNullException.ThrowIfNull(logger);
		_db = db;
		_recipes = recipes;
		_logger = logger;

		var meter = meterFactory.Create(Constants.Metrics.MeterName);
		_created = meter.CreateCounter<long>(Constants.Metrics.Created, unit: "{fact}");
		_updated = meter.CreateCounter<long>(Constants.Metrics.Updated, unit: "{fact}");
		_deleted = meter.CreateCounter<long>(Constants.Metrics.Deleted, unit: "{fact}");
	}

	public async Task<IReadOnlyList<NutritionFactEntity>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		using var activity = _activitySource.StartActivity(nameof(GetAllAsync));
		return await _db.NutritionFacts.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
	}

	public async Task<NutritionFactEntity?> GetByRecipeIdAsync(long recipeId, CancellationToken cancellationToken = default)
	{
		using var activity = _activitySource.StartActivity(nameof(GetByRecipeIdAsync));
		activity?.SetTag(Constants.Tags.RecipeId, recipeId);
		return await _db.NutritionFacts.AsNoTracking().SingleOrDefaultAsync(f => f.RecipeId == recipeId, cancellationToken).ConfigureAwait(false);
	}

	public async Task<NutritionFactEntity?> CreateAsync(NutritionFactEntity fact, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(fact);
		using var activity = _activitySource.StartActivity(nameof(CreateAsync));
		activity?.SetTag(Constants.Tags.RecipeId, fact.RecipeId);

		// Cross-module validation. In a single-image deployment this is an in-process method call
		// against the local RecipeService. In a split deployment Recipe is unreachable in-process
		// and this resolves to RecipeClient (HTTP).
		var recipe = await _recipes.GetByIdAsync(fact.RecipeId, cancellationToken).ConfigureAwait(false);
		if (recipe is null)
		{
			LogRecipeNotFound(fact.RecipeId);
			return null;
		}

		_db.NutritionFacts.Add(fact);
		await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		activity?.SetTag(Constants.Tags.NutritionFactId, fact.Id);
		_created.Add(1,
			new KeyValuePair<string, object?>(Constants.Tags.NutritionFactId, fact.Id),
			new KeyValuePair<string, object?>(Constants.Tags.RecipeId, fact.RecipeId));
		LogCreated(fact.Id, fact.RecipeId);
		return fact;
	}

	public async Task<NutritionFactEntity?> UpdateAsync(long recipeId, NutritionFactEntity delta, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(delta);
		using var activity = _activitySource.StartActivity(nameof(UpdateAsync));
		activity?.SetTag(Constants.Tags.RecipeId, recipeId);

		var entity = await _db.NutritionFacts.SingleOrDefaultAsync(f => f.RecipeId == recipeId, cancellationToken).ConfigureAwait(false);
		if (entity is null) return null;

		entity.CaloriesPerYield = delta.CaloriesPerYield;
		entity.ProteinGrams = delta.ProteinGrams;
		entity.CarbsGrams = delta.CarbsGrams;
		entity.FatGrams = delta.FatGrams;
		entity.FiberGrams = delta.FiberGrams;
		entity.SodiumMilligrams = delta.SodiumMilligrams;

		await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		_updated.Add(1, new KeyValuePair<string, object?>(Constants.Tags.RecipeId, recipeId));
		LogUpdated(recipeId);
		return entity;
	}

	public async Task<bool> DeleteAsync(long recipeId, CancellationToken cancellationToken = default)
	{
		using var activity = _activitySource.StartActivity(nameof(DeleteAsync));
		activity?.SetTag(Constants.Tags.RecipeId, recipeId);

		var entity = await _db.NutritionFacts.SingleOrDefaultAsync(f => f.RecipeId == recipeId, cancellationToken).ConfigureAwait(false);
		if (entity is null) return false;

		_db.NutritionFacts.Remove(entity);
		await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		_deleted.Add(1, new KeyValuePair<string, object?>(Constants.Tags.RecipeId, recipeId));
		LogDeleted(recipeId);
		return true;
	}

	[LoggerMessage(EventId = 4001, Level = LogLevel.Information, Message = "NutritionFact created. Id={Id} RecipeId={RecipeId}")]
	private partial void LogCreated(long id, long recipeId);

	[LoggerMessage(EventId = 4002, Level = LogLevel.Information, Message = "NutritionFact updated. RecipeId={RecipeId}")]
	private partial void LogUpdated(long recipeId);

	[LoggerMessage(EventId = 4003, Level = LogLevel.Information, Message = "NutritionFact deleted. RecipeId={RecipeId}")]
	private partial void LogDeleted(long recipeId);

	[LoggerMessage(EventId = 4004, Level = LogLevel.Warning, Message = "NutritionFact create rejected: Recipe with Id={RecipeId} not found.")]
	private partial void LogRecipeNotFound(long recipeId);
}
