using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BurcinCo.BurcinApp.Data;
using BurcinCo.BurcinApp.Modules.Nutrition.Tracking.NutritionFact.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;
using NutritionFactEntity = BurcinCo.BurcinApp.Models.BurcinDatabase.NutritionFact;

namespace BurcinCo.BurcinApp.Modules.Nutrition.Tracking.NutritionFact.Api;

/// <summary>
/// OData CRUD for <see cref="NutritionFactEntity"/>. Slash-form URLs canonical via attribute routing
/// (<c>/odata/NutritionFact/{recipeId}</c>) — the URL key is the RecipeId since each Recipe gets at
/// most one NutritionFact. Reads go through DbContext for full OData query support; writes route
/// through <see cref="INutritionFactService"/> which performs cross-module recipe-existence check via
/// <c>IRecipeService</c> (in-process when Recipe is in the same deployment, HTTP otherwise).
/// PATCH/PUT/DELETE honor <c>If-Match</c> (412 on stale ETag).
/// </summary>
[Route("odata")]
public sealed class NutritionFactController : ODataController
{
	private readonly BurcinDatabaseDbContext _db;
	private readonly INutritionFactService _service;

	public NutritionFactController(BurcinDatabaseDbContext db, INutritionFactService service)
	{
		_db = db;
		_service = service;
	}

	[EnableQuery]
	[HttpGet("NutritionFact")]
	public IQueryable<NutritionFactEntity> Get() => _db.NutritionFacts.AsNoTracking();

	[EnableQuery]
	[HttpGet("NutritionFact/{key:long}")]
	public SingleResult<NutritionFactEntity> Get([FromODataUri] long key) =>
		SingleResult.Create(_db.NutritionFacts.AsNoTracking().Where(f => f.RecipeId == key));

	[HttpPost("NutritionFact")]
	public async Task<IActionResult> Post([FromBody] NutritionFactEntity fact, CancellationToken cancellationToken)
	{
		if (fact is null) return BadRequest();
		var created = await _service.CreateAsync(fact, cancellationToken).ConfigureAwait(false);
		// 404 here means the cross-module recipe-existence check failed — the FK contract is
		// surfaced as a NotFound at the HTTP boundary so OData clients can distinguish missing-FK
		// from any other 4xx.
		return created is null
			? NotFound($"Recipe with Id={fact.RecipeId} not found.")
			: Created($"odata/NutritionFact/{created.RecipeId}", created);
	}

	[HttpPut("NutritionFact/{key:long}")]
	public async Task<IActionResult> Put([FromODataUri] long key, [FromBody] NutritionFactEntity update, CancellationToken cancellationToken)
	{
		if (update is null) return BadRequest();
		if (await ConcurrencyCheck.PreconditionFailedAsync(Request, _db.NutritionFacts.Where(f => f.RecipeId == key), cancellationToken).ConfigureAwait(false))
		{
			return StatusCode(StatusCodes.Status412PreconditionFailed);
		}
		var updated = await _service.UpdateAsync(key, update, cancellationToken).ConfigureAwait(false);
		return updated is null ? NotFound() : Updated(updated);
	}

	[HttpPatch("NutritionFact/{key:long}")]
	public async Task<IActionResult> Patch([FromODataUri] long key, [FromBody] Delta<NutritionFactEntity> delta, CancellationToken cancellationToken)
	{
		if (delta is null) return BadRequest();
		var entity = await _db.NutritionFacts.SingleOrDefaultAsync(f => f.RecipeId == key, cancellationToken).ConfigureAwait(false);
		if (entity is null) return NotFound();
		if (await ConcurrencyCheck.PreconditionFailedAsync(Request, _db.NutritionFacts.Where(f => f.RecipeId == key), cancellationToken).ConfigureAwait(false))
		{
			return StatusCode(StatusCodes.Status412PreconditionFailed);
		}
		delta.Patch(entity);
		await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		return Updated(entity);
	}

	[HttpDelete("NutritionFact/{key:long}")]
	public async Task<IActionResult> Delete([FromODataUri] long key, CancellationToken cancellationToken)
	{
		if (await ConcurrencyCheck.PreconditionFailedAsync(Request, _db.NutritionFacts.Where(f => f.RecipeId == key), cancellationToken).ConfigureAwait(false))
		{
			return StatusCode(StatusCodes.Status412PreconditionFailed);
		}
		var deleted = await _service.DeleteAsync(key, cancellationToken).ConfigureAwait(false);
		return deleted ? StatusCode(StatusCodes.Status204NoContent) : NotFound();
	}
}
