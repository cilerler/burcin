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
	//[ControllerName(CategoryCodeGroupControllerModelConfiguration.ControllerName)]
#if (OData)
	public class CategoryCodeGroupMappingController : ODataController
#else
	[ApiController]
	[Route("api/[controller]")]
	public class CategoryCodeGroupMappingController : ControllerBase
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
        public CategoryCodeGroupMappingController(IServiceProvider serviceProvider, ILogger<CategoryCodeGroupMappingController> logger, BurcinDatabaseDbContext dbContext)
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
        [ProducesResponseType(typeof(CategoryCodeGroupMapping), StatusCodes.Status201Created)]
        public IActionResult Post([FromBody] CategoryCodeGroupMapping record)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _logger.LogDebug("Inserting {id}", record.Id);
            _dbContext.CategoryCodeGroupMappings.Add(record);
            SaveChanges();
            _logger.LogDebug("Inserted {id}", record.Id);

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
            return Ok(_dbContext.CategoryCodeGroupMappings.AsQueryable());
        }
#else
		[ProducesResponseType(typeof(CategoryCodeGroupMappings[]), StatusCodes.Status200OK)]
		public IActionResult Get()
		{
			var records = _dbContext.CategoryCodeGroupsMappings.ToList();
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
		[ProducesResponseType(typeof(CategoryCodeGroupMapping), StatusCodes.Status200OK)]
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
	        var record = _dbContext.CategoryCodeGroupMappings.Where(r => r.Id == key).AsQueryable();
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
        [ProducesResponseType(typeof(CategoryCodeGroupMapping), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
#if (OData)
		[HttpPut]
	        public IActionResult Put([FromRoute] long key, [FromBody] Delta<CategoryCodeGroupMapping> delta)
#else
		[HttpPut("{key}")]
		public IActionResult Put([FromRoute] long key, [FromBody] CategoryCodeGroupMapping delta)
#endif
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            CategoryCodeGroupMapping record = _dbContext.CategoryCodeGroupMappings.SingleOrDefault(r => r.Id == key);
            if (record == null)
            {
                return NotFound();
            }

            _logger.LogDebug("Updating {id}", record.Id);
#if (OData)
            delta.Put(record);
#else
			record.Code = delta.Code;
			record.CategoryGroupId = delta.CategoryGroupId;
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
        [ProducesResponseType(typeof(CategoryCodeGroupMapping), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
#if (OData)
        [HttpPatch]
	        public IActionResult Patch([FromRoute] long key, [FromBody] Delta<CategoryCodeGroupMapping> delta)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var record = _dbContext.CategoryCodeGroupMappings.SingleOrDefault(r => r.Id == key);
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
		public IActionResult Patch([FromRoute] long key, [FromBody] CategoryCodeGroupMapping delta) => Put(key, delta);
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
            var record = _dbContext.CategoryCodeGroupMappings.SingleOrDefault(r => r.Id == key);
            if (record == null)
            {
                return NotFound();
            }

            if (suspendOnly)
            {
                // CategoryCodeGroup does not have a disable property.
            } else {
                _logger.LogDebug("Deleting {id}", record.Id);
                _dbContext.CategoryCodeGroupMappings.Remove(record);
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
