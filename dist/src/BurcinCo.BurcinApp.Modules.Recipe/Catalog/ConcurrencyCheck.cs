using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog;

/// <summary>
/// Optimistic-concurrency precondition helper for OData controllers in this module. Reads the
/// <c>If-Match</c> header from the request, parses it into an <c>ETag&lt;T&gt;</c>, and applies it
/// as a filter against the supplied query. If no rows survive the filter, the client's ETag
/// doesn't match the current row's concurrency-token values and the action should respond 412.
///
/// The ETag values come from properties declared as concurrency tokens in the EDM. For this
/// project that's <c>RowGuid</c> + <c>RowVersion</c> declared directly on each entity
/// (declared via <c>[ConcurrencyCheck]</c> / <c>[Timestamp]</c>). OData emits the matching
/// <c>ETag</c> response header automatically; well-behaved clients echo it back as <c>If-Match</c>.
///
/// Three behaviors:
///   - No <c>If-Match</c> header → no precondition, returns false (proceed).
///   - <c>If-Match: *</c> → wildcard, returns false (proceed) — caller is asserting "I know this
///     exists, just go ahead". Useful for clients that don't track ETags but want to differentiate
///     update from upsert.
///   - <c>If-Match: "etag"</c> → compare; returns true if no row matches (caller should respond 412).
/// </summary>
internal static class ConcurrencyCheck
{
	public static async Task<bool> PreconditionFailedAsync<T>(
		HttpRequest request,
		IQueryable<T> query,
		CancellationToken cancellationToken) where T : class
	{
		var ifMatch = request.GetTypedHeaders().IfMatch.FirstOrDefault();
		if (ifMatch is null || ifMatch == EntityTagHeaderValue.Any)
		{
			return false;
		}

		var etag = request.GetETag<T>(ifMatch);
		if (etag is null || etag.IsAny)
		{
			return false;
		}

		var filtered = (IQueryable<T>)etag.ApplyTo(query);
		var any = await filtered.AnyAsync(cancellationToken).ConfigureAwait(false);
		return !any;
	}
}
