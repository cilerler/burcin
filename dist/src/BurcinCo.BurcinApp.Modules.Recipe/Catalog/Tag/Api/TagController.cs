using System.Linq;
using BurcinCo.BurcinApp.Modules.Recipe.Catalog.Tag.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using TagEntity = BurcinCo.BurcinApp.Modules.Recipe.Catalog.Tag.Models.Tag;

namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.Tag.Api;

/// <summary>
/// OData controller for the non-database <see cref="TagEntity"/>. Demonstrates that the OData CRUD
/// surface is identical regardless of backing store; the only thing that differs is what
/// <c>TagService.QueryAll()</c> returns. No ETag/If-Match handling here — the in-memory store has
/// no concurrency-token columns, and the demo is about showing OData over a non-EF source rather
/// than full production semantics.
/// </summary>
[Route("odata")]
public sealed class TagController : ODataController
{
	private readonly ITagService _service;

	public TagController(ITagService service)
	{
		_service = service;
	}

	[EnableQuery]
	[HttpGet("Tag")]
	public IQueryable<TagEntity> Get() => _service.QueryAll();

	[EnableQuery]
	[HttpGet("Tag/{key:long}")]
	public SingleResult<TagEntity> Get([FromODataUri] long key) =>
		SingleResult.Create(_service.QueryAll().Where(t => t.Id == key));

	[HttpPost("Tag")]
	public IActionResult Post([FromBody] TagEntity tag)
	{
		if (tag is null) return BadRequest();
		var created = _service.Create(tag);
		return Created($"odata/Tag/{created.Id}", created);
	}

	[HttpPut("Tag/{key:long}")]
	public IActionResult Put([FromODataUri] long key, [FromBody] TagEntity update)
	{
		if (update is null) return BadRequest();
		var replaced = _service.Replace(key, update);
		return replaced is null ? NotFound() : Updated(replaced);
	}

	[HttpPatch("Tag/{key:long}")]
	public IActionResult Patch([FromODataUri] long key, [FromBody] Delta<TagEntity> delta)
	{
		if (delta is null) return BadRequest();
		var entity = _service.GetById(key);
		if (entity is null) return NotFound();
		delta.Patch(entity);
		// Replace re-stamps the dictionary slot so concurrent readers see the post-patch shape.
		_service.Replace(key, entity);
		return Updated(entity);
	}

	[HttpDelete("Tag/{key:long}")]
	public IActionResult Delete([FromODataUri] long key)
	{
		var deleted = _service.Delete(key);
		return deleted ? StatusCode(StatusCodes.Status204NoContent) : NotFound();
	}
}
