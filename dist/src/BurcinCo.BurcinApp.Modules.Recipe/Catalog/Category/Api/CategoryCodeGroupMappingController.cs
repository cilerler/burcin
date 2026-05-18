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
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;
using CategoryCodeGroupMappingEntity = BurcinCo.BurcinApp.Models.BurcinDatabase.CategoryCodeGroupMapping;

namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.Category.Api;

/// <summary>
/// OData read/create/delete for the M:M join entity. EDM still declares the composite key fluently
/// (<c>HasKey(m => new { CategoryCodeId, CategoryGroupId })</c>) so OData metadata + queries stay
/// correct, but routing uses an explicit slash-form URL via attribute routing:
///   <c>/odata/CategoryCodeGroupMapping/{codeId}/{groupId}</c>
/// instead of the OData-canonical paren form
///   <c>/odata/CategoryCodeGroupMapping(CategoryCodeId=1,CategoryGroupId=2)</c>.
/// No PUT or PATCH: composite-key columns are immutable through update (changing them is a
/// different row, which is delete-then-create), audit columns are DB-managed, and there are no
/// other mutable user fields on this join entity. DELETE honors <c>If-Match</c> and returns 412
/// on stale ETag — and because the table has a cascading FK, deletion is unconditionally hard
/// (no soft-delete trigger; cascade from parents is the lifecycle).
/// </summary>
[Route("odata")]
public sealed class CategoryCodeGroupMappingController : ODataController
{
	private readonly BurcinDatabaseDbContext _db;
	private readonly ICategoryService _service;

	public CategoryCodeGroupMappingController(BurcinDatabaseDbContext db, ICategoryService service)
	{
		_db = db;
		_service = service;
	}

	[EnableQuery]
	[HttpGet("CategoryCodeGroupMapping")]
	public IQueryable<CategoryCodeGroupMappingEntity> Get() => _db.CategoryCodeGroupMappings.AsNoTracking();

	[EnableQuery]
	[HttpGet("CategoryCodeGroupMapping/{codeId:long}/{groupId:long}")]
	public IQueryable<CategoryCodeGroupMappingEntity> Get([FromRoute] long codeId, [FromRoute] long groupId) =>
		_db.CategoryCodeGroupMappings.AsNoTracking()
			.Where(m => m.CategoryCodeId == codeId && m.CategoryGroupId == groupId);

	[HttpPost("CategoryCodeGroupMapping")]
	public async Task<IActionResult> Post([FromBody] CategoryCodeGroupMappingEntity mapping, CancellationToken cancellationToken)
	{
		if (mapping is null) return BadRequest();
		var created = await _service.CreateMappingAsync(mapping, cancellationToken).ConfigureAwait(false);
		return Created($"odata/CategoryCodeGroupMapping/{created.CategoryCodeId}/{created.CategoryGroupId}", created);
	}

	[HttpDelete("CategoryCodeGroupMapping/{codeId:long}/{groupId:long}")]
	public async Task<IActionResult> Delete([FromRoute] long codeId, [FromRoute] long groupId, CancellationToken cancellationToken)
	{
		var query = _db.CategoryCodeGroupMappings.Where(m => m.CategoryCodeId == codeId && m.CategoryGroupId == groupId);
		if (await ConcurrencyCheck.PreconditionFailedAsync(Request, query, cancellationToken).ConfigureAwait(false))
		{
			return StatusCode(StatusCodes.Status412PreconditionFailed);
		}
		var deleted = await _service.DeleteMappingAsync(codeId, groupId, cancellationToken).ConfigureAwait(false);
		return deleted ? StatusCode(StatusCodes.Status204NoContent) : NotFound();
	}
}
