using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BurcinCo.BurcinApp.Data;
using BurcinCo.BurcinApp.Modules.Recipe.Catalog.Category.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;
using CategoryCodeEntity = BurcinCo.BurcinApp.Models.BurcinDatabase.CategoryCode;

namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.Category.Api;

[Route("odata")]
public sealed class CategoryCodeController : ODataController
{
	private readonly BurcinDatabaseDbContext _db;
	private readonly ICategoryService _service;

	public CategoryCodeController(BurcinDatabaseDbContext db, ICategoryService service)
	{
		_db = db;
		_service = service;
	}

	[EnableQuery]
	[HttpGet("CategoryCode")]
	public IQueryable<CategoryCodeEntity> Get() => _db.CategoryCodes.AsNoTracking();

	[EnableQuery]
	[HttpGet("CategoryCode/{key:long}")]
	public SingleResult<CategoryCodeEntity> Get([FromODataUri] long key) =>
		SingleResult.Create(_db.CategoryCodes.AsNoTracking().Where(c => c.Id == key));

	[HttpPost("CategoryCode")]
	public async Task<IActionResult> Post([FromBody] CategoryCodeEntity code, CancellationToken cancellationToken)
	{
		if (code is null) return BadRequest();
		var created = await _service.CreateCodeAsync(code, cancellationToken).ConfigureAwait(false);
		return Created($"odata/CategoryCode/{created.Id}", created);
	}

	[HttpPut("CategoryCode/{key:long}")]
	public async Task<IActionResult> Put([FromODataUri] long key, [FromBody] CategoryCodeEntity update, CancellationToken cancellationToken)
	{
		if (update is null) return BadRequest();
		if (await ConcurrencyCheck.PreconditionFailedAsync(Request, _db.CategoryCodes.Where(c => c.Id == key), cancellationToken).ConfigureAwait(false))
		{
			return StatusCode(StatusCodes.Status412PreconditionFailed);
		}
		var updated = await _service.UpdateCodeAsync(key, update, cancellationToken).ConfigureAwait(false);
		return updated is null ? NotFound() : Updated(updated);
	}

	[HttpPatch("CategoryCode/{key:long}")]
	public async Task<IActionResult> Patch([FromODataUri] long key, [FromBody] Delta<CategoryCodeEntity> delta, CancellationToken cancellationToken)
	{
		if (delta is null) return BadRequest();
		var entity = await _db.CategoryCodes.SingleOrDefaultAsync(c => c.Id == key, cancellationToken).ConfigureAwait(false);
		if (entity is null) return NotFound();
		if (await ConcurrencyCheck.PreconditionFailedAsync(Request, _db.CategoryCodes.Where(c => c.Id == key), cancellationToken).ConfigureAwait(false))
		{
			return StatusCode(StatusCodes.Status412PreconditionFailed);
		}
		delta.Patch(entity);
		await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		return Updated(entity);
	}

	[HttpDelete("CategoryCode/{key:long}")]
	public async Task<IActionResult> Delete([FromODataUri] long key, CancellationToken cancellationToken)
	{
		if (await ConcurrencyCheck.PreconditionFailedAsync(Request, _db.CategoryCodes.Where(c => c.Id == key), cancellationToken).ConfigureAwait(false))
		{
			return StatusCode(StatusCodes.Status412PreconditionFailed);
		}
		var deleted = await _service.DeleteCodeAsync(key, cancellationToken).ConfigureAwait(false);
		return deleted ? StatusCode(StatusCodes.Status204NoContent) : NotFound();
	}
}
