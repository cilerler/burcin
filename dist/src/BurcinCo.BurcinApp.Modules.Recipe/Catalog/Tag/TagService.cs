using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using BurcinCo.BurcinApp.Modules.Recipe.Catalog.Tag.Contracts;
using Microsoft.Extensions.Logging;
// The Tag class lives in `BurcinCo.BurcinApp.Modules.Recipe.Catalog.Tag.Models.Tag`, but this file
// is in the parent `Catalog.Tag` namespace where the bare name `Tag` resolves to the namespace, not the
// type. Aliasing as TagEntity matches the Chef/Recipe convention used elsewhere in this module.
using TagEntity = BurcinCo.BurcinApp.Modules.Recipe.Catalog.Tag.Models.Tag;

namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.Tag;

/// <summary>
/// In-memory <see cref="ITagService"/>. Single instance per process — registered as singleton.
/// Backing store: <see cref="ConcurrentDictionary{TKey,TValue}"/> + monotonic id generator. Survives
/// across requests within a process; lost on restart. Good enough for a demo, not for anything else.
///
/// Observability mirrors the DB-backed services so the template's traces/metrics story is consistent
/// regardless of where an entity lives.
/// </summary>
internal sealed partial class TagService : ITagService
{
	private static readonly ActivitySource _activitySource = new(Constants.Activities.ActivitySourceName);

	private readonly ConcurrentDictionary<long, TagEntity> _store = new();
	private readonly ILogger<TagService> _logger;

	private readonly Counter<long> _created;
	private readonly Counter<long> _updated;
	private readonly Counter<long> _deleted;

	private long _nextId;

	public TagService(IMeterFactory meterFactory, ILogger<TagService> logger)
	{
		ArgumentNullException.ThrowIfNull(meterFactory);
		ArgumentNullException.ThrowIfNull(logger);
		_logger = logger;

		var meter = meterFactory.Create(Constants.Metrics.MeterName);
		_created = meter.CreateCounter<long>(Constants.Metrics.Created, unit: "{tag}");
		_updated = meter.CreateCounter<long>(Constants.Metrics.Updated, unit: "{tag}");
		_deleted = meter.CreateCounter<long>(Constants.Metrics.Deleted, unit: "{tag}");
	}

	public IQueryable<TagEntity> QueryAll()
	{
		using var activity = _activitySource.StartActivity(nameof(QueryAll));
		// Snapshot to a list so the OData provider sees a stable sequence — Values is a live view.
		return _store.Values.ToList().AsQueryable();
	}

	public IReadOnlyList<TagEntity> GetAll()
	{
		using var activity = _activitySource.StartActivity(nameof(GetAll));
		return _store.Values.ToList();
	}

	public TagEntity? GetById(long id)
	{
		using var activity = _activitySource.StartActivity(nameof(GetById));
		activity?.SetTag(Constants.Tags.TagId, id);
		return _store.TryGetValue(id, out var tag) ? tag : null;
	}

	public TagEntity Create(TagEntity tag)
	{
		ArgumentNullException.ThrowIfNull(tag);
		using var activity = _activitySource.StartActivity(nameof(Create));
		tag.Id = Interlocked.Increment(ref _nextId);
		tag.CreatedAt = DateTimeOffset.UtcNow;
		_store[tag.Id] = tag;
		activity?.SetTag(Constants.Tags.TagId, tag.Id);
		_created.Add(1, new KeyValuePair<string, object?>(Constants.Tags.TagId, tag.Id));
		LogCreated(tag.Id, tag.Name);
		return tag;
	}

	public TagEntity? Replace(long id, TagEntity update)
	{
		ArgumentNullException.ThrowIfNull(update);
		using var activity = _activitySource.StartActivity(nameof(Replace));
		activity?.SetTag(Constants.Tags.TagId, id);
		if (!_store.ContainsKey(id)) return null;
		update.Id = id;
		_store[id] = update;
		_updated.Add(1, new KeyValuePair<string, object?>(Constants.Tags.TagId, id));
		LogUpdated(id);
		return update;
	}

	public bool Delete(long id)
	{
		using var activity = _activitySource.StartActivity(nameof(Delete));
		activity?.SetTag(Constants.Tags.TagId, id);
		var removed = _store.TryRemove(id, out _);
		if (removed)
		{
			_deleted.Add(1, new KeyValuePair<string, object?>(Constants.Tags.TagId, id));
			LogDeleted(id);
		}
		return removed;
	}

	[LoggerMessage(EventId = 3201, Level = LogLevel.Information, Message = "Tag created. Id={TagId} Name={Name}")]
	private partial void LogCreated(long tagId, string name);

	[LoggerMessage(EventId = 3202, Level = LogLevel.Information, Message = "Tag updated. Id={TagId}")]
	private partial void LogUpdated(long tagId);

	[LoggerMessage(EventId = 3203, Level = LogLevel.Information, Message = "Tag deleted. Id={TagId}")]
	private partial void LogDeleted(long tagId);
}
