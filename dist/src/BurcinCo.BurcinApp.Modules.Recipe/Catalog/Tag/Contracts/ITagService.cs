using System.Collections.Generic;
using System.Linq;
using TagEntity = BurcinCo.BurcinApp.Modules.Recipe.Catalog.Tag.Models.Tag;

namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.Tag.Contracts;

/// <summary>
/// In-memory Tag store contract. Returns synchronously because the backing store doesn't do I/O —
/// keeping the surface non-async makes it obvious to callers that this isn't a real persistence
/// layer. If you swap the store for a Redis or HTTP-backed implementation later, change this
/// interface to async at the same time so mistaken sync-over-async usage doesn't survive.
///
/// <see cref="QueryAll"/> returns <see cref="IQueryable{T}"/> so the OData controller can compose
/// <c>$filter</c>/<c>$orderby</c>/<c>$top</c>/<c>$skip</c> against the in-memory snapshot.
/// </summary>
// Public because TagController (public for MVC discovery) takes this in its constructor.
public interface ITagService
{
	IQueryable<TagEntity> QueryAll();

	IReadOnlyList<TagEntity> GetAll();

	TagEntity? GetById(long id);

	TagEntity Create(TagEntity tag);

	TagEntity? Replace(long id, TagEntity update);

	bool Delete(long id);
}
