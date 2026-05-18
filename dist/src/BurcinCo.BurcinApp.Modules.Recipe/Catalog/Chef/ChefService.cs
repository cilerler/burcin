using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using BurcinCo.BurcinApp.Data;
using BurcinCo.BurcinApp.Modules.Recipe.Catalog.Chef.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ChefEntity = BurcinCo.BurcinApp.Models.BurcinDatabase.Chef;

namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.Chef;

internal sealed partial class ChefService : IChefService
{
	private static readonly ActivitySource _activitySource = new(Constants.Activities.ActivitySourceName);

	private readonly BurcinDatabaseDbContext _db;
	private readonly ILogger<ChefService> _logger;

	private readonly Counter<long> _created;
	private readonly Counter<long> _updated;
	private readonly Counter<long> _deleted;

	public ChefService(
		BurcinDatabaseDbContext db,
		IMeterFactory meterFactory,
		ILogger<ChefService> logger)
	{
		ArgumentNullException.ThrowIfNull(db);
		ArgumentNullException.ThrowIfNull(meterFactory);
		ArgumentNullException.ThrowIfNull(logger);
		_db = db;
		_logger = logger;

		var meter = meterFactory.Create(Constants.Metrics.MeterName);
		_created = meter.CreateCounter<long>(Constants.Metrics.Created, unit: "{chef}");
		_updated = meter.CreateCounter<long>(Constants.Metrics.Updated, unit: "{chef}");
		_deleted = meter.CreateCounter<long>(Constants.Metrics.Deleted, unit: "{chef}");
	}

	public async Task<IReadOnlyList<ChefEntity>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		using var activity = _activitySource.StartActivity(nameof(GetAllAsync));
		return await _db.Chefs.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
	}

	public async Task<ChefEntity?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
	{
		using var activity = _activitySource.StartActivity(nameof(GetByIdAsync));
		activity?.SetTag(Constants.Tags.ChefId, id);
		return await _db.Chefs.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, cancellationToken).ConfigureAwait(false);
	}

	public async Task<ChefEntity> CreateAsync(ChefEntity chef, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(chef);
		using var activity = _activitySource.StartActivity(nameof(CreateAsync));
		_db.Chefs.Add(chef);
		await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		activity?.SetTag(Constants.Tags.ChefId, chef.Id);
		_created.Add(1, new KeyValuePair<string, object?>(Constants.Tags.ChefId, chef.Id));
		LogCreated(chef.Id, chef.Name);
		return chef;
	}

	public async Task<ChefEntity?> UpdateAsync(long id, ChefEntity delta, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(delta);
		using var activity = _activitySource.StartActivity(nameof(UpdateAsync));
		activity?.SetTag(Constants.Tags.ChefId, id);
		var entity = await _db.Chefs.SingleOrDefaultAsync(c => c.Id == id, cancellationToken).ConfigureAwait(false);
		if (entity is null) return null;

		entity.Name = delta.Name;
		entity.Url = delta.Url;
		await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		_updated.Add(1, new KeyValuePair<string, object?>(Constants.Tags.ChefId, id));
		LogUpdated(id);
		return entity;
	}

	public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
	{
		using var activity = _activitySource.StartActivity(nameof(DeleteAsync));
		activity?.SetTag(Constants.Tags.ChefId, id);
		var entity = await _db.Chefs.SingleOrDefaultAsync(c => c.Id == id, cancellationToken).ConfigureAwait(false);
		if (entity is null) return false;

		_db.Chefs.Remove(entity);
		await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		_deleted.Add(1, new KeyValuePair<string, object?>(Constants.Tags.ChefId, id));
		LogDeleted(id);
		return true;
	}

	[LoggerMessage(EventId = 3101, Level = LogLevel.Information, Message = "Chef created. Id={ChefId} Name={Name}")]
	private partial void LogCreated(long chefId, string name);

	[LoggerMessage(EventId = 3102, Level = LogLevel.Information, Message = "Chef updated. Id={ChefId}")]
	private partial void LogUpdated(long chefId);

	[LoggerMessage(EventId = 3103, Level = LogLevel.Information, Message = "Chef deleted. Id={ChefId}")]
	private partial void LogDeleted(long chefId);
}
