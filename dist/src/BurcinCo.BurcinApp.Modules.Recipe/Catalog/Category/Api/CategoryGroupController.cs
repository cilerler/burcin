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
using CategoryGroupEntity = BurcinCo.BurcinApp.Models.Zignec.CategoryGroup;

namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.Category.Api;

[Route("odata")]
public sealed class CategoryGroupController : ODataController
{
	private readonly BurcinDatabaseDbContext _db;
	private readonly ICategoryService _service;

	public CategoryGroupController(BurcinDatabaseDbContext db, ICategoryService service)
	{
		_db = db;
		_service = service;
	}

	[EnableQuery]
	[HttpGet("CategoryGroup")]
	public IQueryable<CategoryGroupEntity> Get() => _db.CategoryGroups.AsNoTracking();

	[EnableQuery]
	[HttpGet("CategoryGroup/{key:long}")]
	public SingleResult<CategoryGroupEntity> Get([FromODataUri] long key) =>
		SingleResult.Create(_db.CategoryGroups.AsNoTracking().Where(g => g.Id == key));

	[HttpPost("CategoryGroup")]
	public async Task<IActionResult> Post([FromBody] CategoryGroupEntity group, CancellationToken cancellationToken)
	{
		if (group is null) return BadRequest();
		var created = await _service.CreateGroupAsync(group, cancellationToken).ConfigureAwait(false);
		return Created($"odata/CategoryGroup/{created.Id}", created);
	}

	[HttpPut("CategoryGroup/{key:long}")]
	public async Task<IActionResult> Put([FromODataUri] long key, [FromBody] CategoryGroupEntity update, CancellationToken cancellationToken)
	{
		if (update is null) return BadRequest();
		if (await ConcurrencyCheck.PreconditionFailedAsync(Request, _db.CategoryGroups.Where(g => g.Id == key), cancellationToken).ConfigureAwait(false))
		{
			return StatusCode(StatusCodes.Status412PreconditionFailed);
		}
		var updated = await _service.UpdateGroupAsync(key, update, cancellationToken).ConfigureAwait(false);
		return updated is null ? NotFound() : Updated(updated);
	}

	[HttpPatch("CategoryGroup/{key:long}")]
	public async Task<IActionResult> Patch([FromODataUri] long key, [FromBody] Delta<CategoryGroupEntity> delta, CancellationToken cancellationToken)
	{
		if (delta is null) return BadRequest();
		var entity = await _db.CategoryGroups.SingleOrDefaultAsync(g => g.Id == key, cancellationToken).ConfigureAwait(false);
		if (entity is null) return NotFound();
		if (await ConcurrencyCheck.PreconditionFailedAsync(Request, _db.CategoryGroups.Where(g => g.Id == key), cancellationToken).ConfigureAwait(false))
		{
			return StatusCode(StatusCodes.Status412PreconditionFailed);
		}
		delta.Patch(entity);
		await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		return Updated(entity);
	}

	[HttpDelete("CategoryGroup/{key:long}")]
	public async Task<IActionResult> Delete([FromODataUri] long key, CancellationToken cancellationToken)
	{
		if (await ConcurrencyCheck.PreconditionFailedAsync(Request, _db.CategoryGroups.Where(g => g.Id == key), cancellationToken).ConfigureAwait(false))
		{
			return StatusCode(StatusCodes.Status412PreconditionFailed);
		}
		var deleted = await _service.DeleteGroupAsync(key, cancellationToken).ConfigureAwait(false);
		return deleted ? StatusCode(StatusCodes.Status204NoContent) : NotFound();
	}
}
