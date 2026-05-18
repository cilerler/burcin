using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BurcinCo.BurcinApp.Data;
using BurcinCo.BurcinApp.Modules.Recipe.Abstractions.Interfaces;
using BurcinCo.BurcinApp.Modules.Recipe.Abstractions.Requests;
using BurcinCo.BurcinApp.Modules.Recipe.Catalog;
using BurcinCo.BurcinApp.Modules.Recipe.Catalog.Recipe.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;
using RecipeEntity = BurcinCo.BurcinApp.Models.Zignec.Recipe;

namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.Recipe.Api;

/// <summary>
/// OData CRUD for <see cref="RecipeEntity"/>. Slash-form URLs canonical via attribute routing.
/// POST/PUT translate the entity payload into the cross-module-public <see cref="RecipeCreateRequest"/>
/// DTO so the service contract stays free of EF entity types — that's the same DTO sibling-module HTTP
/// clients use, keeping the in-process and remote call paths symmetrical.
/// PATCH/PUT/DELETE honor <c>If-Match</c> (412 on stale ETag).
/// </summary>
[Route("odata")]
public sealed class RecipeController : ODataController
{
	private readonly BurcinDatabaseDbContext _db;
	private readonly IRecipeService _service;

	public RecipeController(BurcinDatabaseDbContext db, IRecipeService service)
	{
		_db = db;
		_service = service;
	}

	[EnableQuery]
	[HttpGet("Recipe")]
	public IQueryable<RecipeEntity> Get() => _db.Recipes.AsNoTracking();

	[EnableQuery]
	[HttpGet("Recipe/{key:long}")]
	public SingleResult<RecipeEntity> Get([FromODataUri] long key) =>
		SingleResult.Create(_db.Recipes.AsNoTracking().Where(r => r.Id == key));

	[HttpPost("Recipe")]
	public async Task<IActionResult> Post([FromBody] RecipeEntity recipe, CancellationToken cancellationToken)
	{
		if (recipe is null) return BadRequest();
		var view = await _service.CreateAsync(
			new RecipeCreateRequest(recipe.ChefId, recipe.Name, recipe.Url, recipe.Yield, recipe.GramPerYield, recipe.CategoryCode),
			cancellationToken).ConfigureAwait(false);
		// Re-fetch the persisted entity so OData returns the full server-computed shape (RowGuid, timestamps, etc.).
		var persisted = await _db.Recipes.SingleOrDefaultAsync(r => r.Id == view.Id, cancellationToken).ConfigureAwait(false);
		return Created($"odata/Recipe/{view.Id}", persisted!);
	}

	[HttpPut("Recipe/{key:long}")]
	public async Task<IActionResult> Put([FromODataUri] long key, [FromBody] RecipeEntity update, CancellationToken cancellationToken)
	{
		if (update is null) return BadRequest();
		if (await ConcurrencyCheck.PreconditionFailedAsync(Request, _db.Recipes.Where(r => r.Id == key), cancellationToken).ConfigureAwait(false))
		{
			return StatusCode(StatusCodes.Status412PreconditionFailed);
		}
		var view = await _service.UpdateAsync(
			key,
			new RecipeCreateRequest(update.ChefId, update.Name, update.Url, update.Yield, update.GramPerYield, update.CategoryCode),
			cancellationToken).ConfigureAwait(false);
		if (view is null) return NotFound();
		var persisted = await _db.Recipes.SingleOrDefaultAsync(r => r.Id == view.Id, cancellationToken).ConfigureAwait(false);
		return Updated(persisted!);
	}

	[HttpPatch("Recipe/{key:long}")]
	public async Task<IActionResult> Patch([FromODataUri] long key, [FromBody] Delta<RecipeEntity> delta, CancellationToken cancellationToken)
	{
		if (delta is null) return BadRequest();
		var entity = await _db.Recipes.SingleOrDefaultAsync(r => r.Id == key, cancellationToken).ConfigureAwait(false);
		if (entity is null) return NotFound();
		if (await ConcurrencyCheck.PreconditionFailedAsync(Request, _db.Recipes.Where(r => r.Id == key), cancellationToken).ConfigureAwait(false))
		{
			return StatusCode(StatusCodes.Status412PreconditionFailed);
		}
		delta.Patch(entity);
		await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
		return Updated(entity);
	}

	[HttpDelete("Recipe/{key:long}")]
	public async Task<IActionResult> Delete([FromODataUri] long key, CancellationToken cancellationToken)
	{
		if (await ConcurrencyCheck.PreconditionFailedAsync(Request, _db.Recipes.Where(r => r.Id == key), cancellationToken).ConfigureAwait(false))
		{
			return StatusCode(StatusCodes.Status412PreconditionFailed);
		}
		var deleted = await _service.DeleteAsync(key, cancellationToken).ConfigureAwait(false);
		return deleted ? StatusCode(StatusCodes.Status204NoContent) : NotFound();
	}

	/// <summary>
	/// OData function bound to Recipe. Returns a derived <see cref="RecipeSummary"/> joining data
	/// from Recipe, Chef, and CategoryCode in one query — saves the client from issuing $expand
	/// against multiple navigation properties just to render a "recipe card". Read-only; safe for
	/// GET, idempotent, no side effects (that's the function vs. action distinction in OData).
	///
	/// EDM declaration in <c>ODataExtensions.AddRecipeEntitySets</c>:
	///   <c>recipeEntitySet.EntityType.Function("GetSummary").Returns&lt;RecipeSummary&gt;();</c>
	///
	/// URL: <c>GET /odata/Recipe/{key}/GetSummary</c> (slash-form via attribute routing).
	/// OData's metadata also exposes the canonical paren form <c>/odata/Recipe(123)/Default.GetSummary()</c>.
	/// </summary>
	[HttpGet("Recipe/{key:long}/GetSummary")]
	public async Task<IActionResult> GetSummary([FromRoute] long key, CancellationToken cancellationToken)
	{
		var summary = await _db.Recipes
			.AsNoTracking()
			.Where(r => r.Id == key)
			.Select(r => new RecipeSummary
			{
				RecipeId = r.Id,
				RecipeName = r.Name,
				ChefName = r.Chef.Name,
				CategoryName = r.CategoryCodeNavigation == null ? null : r.CategoryCodeNavigation.Name,
				GramTotal = r.GramPerYield * r.Yield,
			})
			.SingleOrDefaultAsync(cancellationToken)
			.ConfigureAwait(false);

		return summary is null ? NotFound() : Ok(summary);
	}
}
