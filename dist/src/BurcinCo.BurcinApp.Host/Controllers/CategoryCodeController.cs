using System;
using System.Linq;
#if (OData)
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Deltas;
#endif
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using BurcinCo.BurcinApp.Data;
using BurcinCo.BurcinApp.Models.BurcinDatabase;
using Microsoft.AspNetCore.Http;

namespace BurcinCo.BurcinApp.Host.Controllers
{

	/// <summary>
	/// Represents a RESTful service.
	/// </summary>
	//[ControllerName(CategoryCodeControllerModelConfiguration.ControllerName)]
#if (OData)
	public class CategoryCodeController : ODataController
#else
	[ApiController]
	[Route("api/[controller]")]
	public class CategoryCodeController : ControllerBase
#endif
	{
		private readonly IServiceProvider _serviceProvider;
		private readonly ILogger _logger;
		private readonly BurcinDatabaseDbContext _dbContext;

		/// <summary>
		/// Not sure why do I have to fill this
		/// </summary>
		/// <param name="serviceProvider"></param>
		/// <param name="logger"></param>
		/// <param name="dbContext"></param>
		public CategoryCodeController(IServiceProvider serviceProvider, ILogger<CategoryCodeController> logger, BurcinDatabaseDbContext dbContext)
		{
			_serviceProvider = serviceProvider;
			_logger = logger;
			_dbContext = dbContext;
		}

		/// <summary>
		/// Inserts a new record.
		/// </summary>
		/// <param name="record">The record to place.</param>
		/// <returns>The created record.</returns>
		/// <response code="400">The record is invalid.</response>
		/// <response code="201">The record was successfully placed.</response>
		[HttpPost]
		[Produces("application/json")]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		[ProducesResponseType(typeof(CategoryCode), StatusCodes.Status201Created)]
		public IActionResult Post([FromBody] CategoryCode record)
		{
			if (!ModelState.IsValid)
			{
				return BadRequest(ModelState);
			}

			_logger.LogDebug("Inserting {id}", record.Id);
			_dbContext.CategoryCodes.Add(record);
			SaveChanges();
			_logger.LogDebug("Inserted {id}", record.Id);

			// if (record.Recipes != null)
			// {
			// 	_logger.LogDebug("Inserting recipes", record.Id);
			// 	_dbContext.Recipes.AddRange(record.Recipes);
			// 	_logger.LogDebug("Inserted recipes", record.Id);
			// 	SaveChanges();
			// }
#if (OData)
			return Created(record);
#else
			return Ok(record);
#endif
		}

		/// <summary>
		/// Retrieves all records
		/// </summary>
		/// <returns>All available records.</returns>
		/// <response code="400">The parameters are invalid.</response>
		/// <response code="404">The record does not exist.</response>
		/// <response code="200">The record was successfully retrieved.</response>
		[HttpGet]
		[Produces("application/json")]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
#if (OData)
		[EnableQuery(MaxTop = 100, MaxExpansionDepth = 3)]
		[ProducesResponseType(StatusCodes.Status200OK)]
		public IActionResult Get()
		{
			return Ok(_dbContext.CategoryCodes.AsQueryable());
		}
#else
		[ProducesResponseType(typeof(CategoryCode[]), StatusCodes.Status200OK)]
		public IActionResult Get()
		{
			var records = _dbContext.CategoryCodes.ToList();
			return Ok(records);
		}
#endif

		/// <summary>
		/// Retrieves a single specific record
		/// </summary>
		/// <param name="key">The requested record identifier.</param>
		/// <returns>The requested record</returns>
		/// <response code="400">The parameters are invalid.</response>
		/// <response code="404">The record does not exist.</response>
		/// <response code="200">The record was successfully retrieved.</response>
		[Produces("application/json")]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(CategoryCode), StatusCodes.Status200OK)]
#if (OData)
		[HttpGet]
		[EnableQuery(
			AllowedQueryOptions = AllowedQueryOptions.All,
			AllowedArithmeticOperators = AllowedArithmeticOperators.All,
			AllowedFunctions = AllowedFunctions.AllFunctions,
			AllowedLogicalOperators = AllowedLogicalOperators.All,
			MaxOrderByNodeCount = 2,
			MaxTop = 100,
			MaxSkip = 100000,
			MaxNodeCount = 200,
			MaxAnyAllExpressionDepth = 3,
			MaxExpansionDepth = 3
		)]
		public IActionResult Get([FromRoute] long key)
#else
		[HttpGet("{key}")]
		public IActionResult Get([FromRoute] long key)
#endif
		{
			var record = _dbContext.CategoryCodes.Where(r => r.Id == key).AsQueryable();
			if (record == null || !record.Any())
			{
				return NotFound();
			}

			return Ok(record);
		}

		/// <summary>
		/// Updates an existing record.
		/// </summary>
		/// <param name="key">The requested record identifier.</param>
		/// <param name="delta">The partial record to update.</param>
		/// <returns>The created record.</returns>
		/// <response code="400">The record is invalid.</response>
		/// <response code="404">The record does not exist.</response>
		/// <response code="204">The record was successfully updated.</response>
		[Produces("application/json")]
		[ProducesResponseType(typeof(CategoryCode), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
#if (OData)
		[HttpPut]
		public IActionResult Put([FromRoute] long key, [FromBody] Delta<CategoryCode> delta)
#else
		[HttpPut("{key}")]
		public IActionResult Put([FromRoute] long key, [FromBody] CategoryCode delta)
#endif
		{
			if (!ModelState.IsValid)
			{
				return BadRequest(ModelState);
			}

			CategoryCode record = _dbContext.CategoryCodes.SingleOrDefault(r => r.Id == key);
			if (record == null)
			{
				return NotFound();
			}

			_logger.LogDebug("Updating {id}", record.Id);
#if (OData)
			delta.Put(record);
#else
			record.Code = delta.Code;
			record.Name = delta.Name;
#endif
			SaveChanges();
			_logger.LogDebug("Updated {id}", record.Id);

#if (OData)
			return Updated(record);
#else
			return Ok(record);
#endif
		}

		/// <summary>
		/// Updates an existing record from partial data
		/// </summary>
		/// <param name="key">The requested record identifier.</param>
		/// <param name="delta">The partial record to update.</param>
		/// <returns>The created record.</returns>
		/// <response code="400">The record is invalid.</response>
		/// <response code="404">The record does not exist.</response>
		/// <response code="204">The record was successfully updated.</response>
		[Produces("application/json")]
		[ProducesResponseType(typeof(CategoryCode), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
#if (OData)
		[HttpPatch]
		public IActionResult Patch([FromRoute] long key, [FromBody] Delta<CategoryCode> delta)
		{
			if (!ModelState.IsValid)
			{
				return BadRequest(ModelState);
			}

			var record = _dbContext.CategoryCodes.SingleOrDefault(r => r.Id == key);
			if (record == null)
			{
				return NotFound();
			}

			_logger.LogDebug("Patching {id}", record.Id);
			delta.Patch(record);
			SaveChanges();
			_logger.LogDebug("Patched {id}", record.Id);

			return Updated(record);
		}
#else
		[HttpPatch("{key}")]
		public IActionResult Patch([FromRoute] long key, [FromBody] CategoryCode delta) => Put(key, delta);
#endif

		/// <summary>
		/// Cancels a record.
		/// </summary>
		/// <param name="key">The record to cancel.</param>
		/// <param name="suspendOnly">Indicates if the record should only be suspended.</param>
		/// <returns>None</returns>
		/// <response code="404">The record does not exist.</response>
		/// <response code="204">The record was successfully canceled.</response>
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
#if (OData)
		[HttpDelete]
		public IActionResult Delete([FromRoute] long key, bool suspendOnly)
#else
		[HttpDelete("{key}")]
		public IActionResult Delete([FromRoute] long key, bool suspendOnly)
#endif
		{
			var record = _dbContext.CategoryCodes.SingleOrDefault(r => r.Id == key);
			if (record == null)
			{
				return NotFound();
			}

			if (suspendOnly)
			{
				// CategoryCode does not have a disable property.
			} else {
				_logger.LogDebug("Deleting {id}", record.Id);
				_dbContext.CategoryCodes.Remove(record);
				SaveChanges();
				_logger.LogDebug("Deleted {id}", record.Id);
			}

			return NoContent();
		}

		#region InternalFunctions

		private void SaveChanges()
		{
			_dbContext.SaveChanges();
		}
		#endregion
	}
}
