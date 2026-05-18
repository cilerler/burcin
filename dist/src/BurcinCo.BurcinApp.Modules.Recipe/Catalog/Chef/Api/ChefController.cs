using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BurcinCo.BurcinApp.Data;
using BurcinCo.BurcinApp.Modules.Recipe.Catalog.Chef.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;
using ChefEntity = BurcinCo.BurcinApp.Models.Zignec.Chef;

namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.Chef.Api;

/// <summary>
/// OData CRUD for <see cref="ChefEntity"/>. Slash-form URLs are canonical (<c>/odata/Chef/1</c>) via
/// explicit attribute routing — OData also auto-registers the parens form (<c>/odata/Chef(1)</c>) via
/// convention, both reach the same actions. We document and use the slash form because it aggregates
/// cleanly in telemetry (`http.url` groups as `/odata/Chef/{key}` instead of distinct literals).
///
/// ETag / optimistic concurrency: the EDM declares RowGuid + RowVersion as concurrency tokens
/// (via [ConcurrencyCheck] / [Timestamp] declared on each entity). OData emits ETag headers on responses
/// automatically; PATCH/PUT/DELETE check If-Match and return 412 on mismatch.
/// </summary>
[Route("odata")]
public sealed class ChefController : ODataController
{
	private readonly BurcinDatabaseDbContext _db;
	private readonly IChefService _service;

	public ChefController(BurcinDatabaseDbContext db, IChefService service)
	{
		_db = db;
		_service = service;
	}

	[EnableQuery]
	[HttpGet("Chef")]
	public IQueryable<ChefEntity> Get() => _db.Chefs.AsNoTracking();

	[EnableQuery]
	[HttpGet("Chef/{key:long}")]
	public SingleResult<ChefEntity> Get([FromODataUri] long key) =>
		SingleResult.Create(_db.Chefs.AsNoTracking().Where(c => c.Id == key));

	[HttpPost("Chef")]
	public async Task<IActionResult> Post([FromBody] ChefEntity chef, CancellationToken cancellationToken)
	{
		if (chef is null) return BadRequest();
		var created = await _service.CreateAsync(chef, cancellationToken).ConfigureAwait(false);
		// Slash-form Location header to match canonical URL shape.
		return Created($"odata/Chef/{created.Id}", created);
	}

	[HttpPut("Chef/{key:long}")]
	public async Task<IActionResult> Put([FromODataUri] long key, [FromBody] ChefEntity update, CancellationToken cancellationToken)
	{
		if (update is null) return BadRequest();
		// Optimistic-concurrency precondition: client supplies If-Match with the entity's ETag.
		// If the ETag doesn't match the current row's, this method returns 412 before touching state.
		if (await ConcurrencyCheck.PreconditionFailedAsync(Request, _db.Chefs.Where(c => c.Id == key), cancellationToken).ConfigureAwait(false))
		{
			return StatusCode(StatusCodes.Status412PreconditionFailed);
		}
		var updated = await _service.UpdateAsync(key, update, cancellationToken).ConfigureAwait(false);
		return updated is null ? NotFound() : Updated(updated);
	}

	[HttpPatch("Chef/{key:long}")]
	public async Task<IActionResult> Patch([FromODataUri] long key, [FromBody] Delta<ChefEntity> delta, CancellationToken cancellationToken)
	{
		if (delta is null) return BadRequest();
		var entity = await _db.Chefs.SingleOrDefaultAsync(c => c.Id == key, cancellationToken).ConfigureAwait(false);
		if (entity is null) return NotFound();
		if (await ConcurrencyCheck.PreconditionFailedAsync(Request, _db.Chefs.Where(c => c.Id == key), cancellationToken).ConfigureAwait(false))
		{
			return StatusCode(StatusCodes.Status412PreconditionFailed);
		}
		// Delta<T> applies only the properties present in the request body — leaves all other fields alone.
		// That's how OData expresses RFC 7396-style partial updates without a special wire format.
		delta.Patch(entity);
		await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		return Updated(entity);
	}

	[HttpDelete("Chef/{key:long}")]
	public async Task<IActionResult> Delete([FromODataUri] long key, CancellationToken cancellationToken)
	{
		if (await ConcurrencyCheck.PreconditionFailedAsync(Request, _db.Chefs.Where(c => c.Id == key), cancellationToken).ConfigureAwait(false))
		{
			return StatusCode(StatusCodes.Status412PreconditionFailed);
		}
		var deleted = await _service.DeleteAsync(key, cancellationToken).ConfigureAwait(false);
		return deleted ? StatusCode(StatusCodes.Status204NoContent) : NotFound();
	}
}
