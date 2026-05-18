using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace BurcinCo.BurcinApp.Modules.Nutrition.Tracking;

/// <summary>
/// Optimistic-concurrency precondition helper for OData controllers in this module. Same shape as
/// the Recipe module's helper — duplicated rather than shared because the only consumers are
/// per-module OData controllers and the package surface (Microsoft.AspNetCore.OData) is module-scoped.
/// See the Recipe-module variant for the full behavior contract.
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
