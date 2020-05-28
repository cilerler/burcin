using System;
using System.Linq;
using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Query;
using Microsoft.AspNet.OData.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.OData;
using Burcin.Data;
using Burcin.Models.BurcinDatabase;
using Microsoft.AspNetCore.Http;

namespace Burcin.Api.Controllers
{

    /// <summary>
    /// Represents a RESTful service.
    /// </summary>
    //[ControllerName(ChefControllerModelConfiguration.ControllerName)]
    [ODataRoutePrefix(nameof(RecipeExpansion))]
    public class RecipeExpansionController : ODataController
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
        public RecipeExpansionController(IServiceProvider serviceProvider, ILogger<RecipeExpansionController> logger, BurcinDatabaseDbContext dbContext)
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
        [ODataRoute]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(RecipeExpansion), StatusCodes.Status201Created)]
        public IActionResult Post([FromBody] RecipeExpansion record)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _logger.LogDebug("Inserting {id}", record.Id);
            _dbContext.RecipeExpansions.Add(record);
            SaveChanges();
            _logger.LogDebug("Inserted {id}", record.Id);

   //         if (record.RecipeExpansions != null)
   //         {
	  //          _logger.LogDebug("Inserting RecipeExpansions", record.Id);
	  //          _dbContext.RecipeExpansions.AddRange(record.RecipeExpansions);
	  //          _logger.LogDebug("Inserted RecipeExpansions", record.Id);
			//	SaveChanges();
			//}

			return Created(record);
        }

        /// <summary>
        /// Retrieves all records
        /// </summary>
        /// <param name="options">The current OData query options.</param>
        /// <returns>All available records.</returns>
        /// <response code="400">The parameters are invalid.</response>
        /// <response code="404">The record does not exist.</response>
        /// <response code="200">The record was successfully retrieved.</response>
        [HttpGet]
        [ODataRoute]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        //[ProducesResponseType(typeof(ODataValue<IEnumerable<Chef>>), StatusCodes.Status200OK)]
        public IActionResult Get(ODataQueryOptions<RecipeExpansion> options)
        {
            var validationSettings = new ODataValidationSettings()
            {
                // AllowedOrderByProperties = { "Id", "uploadDate" },
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
            };

            try
            {
                options.Validate(validationSettings);
            }
            catch (ODataException oe)
            {
                return BadRequest(oe);
            }

            var query = _dbContext.RecipeExpansions.AsQueryable();
            var records = options.ApplyTo(query);
            if (records == null || !records.Any())
            {
                return NotFound();
            }

            return Ok(records);
        }

        /// <summary>
        /// Retrieves a single specific record
        /// </summary>
        /// <param name="key">The requested record identifier.</param>
        /// <returns>The requested record</returns>
        /// <response code="400">The parameters are invalid.</response>
        /// <response code="404">The record does not exist.</response>
        /// <response code="200">The record was successfully retrieved.</response>
        [HttpGet]
        [ODataRoute("({key})")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(RecipeExpansion), StatusCodes.Status200OK)]
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
        public IActionResult Get([FromODataUri] long key)
        {
            var record = _dbContext.RecipeExpansions.Where(r => r.Id == key).AsQueryable();
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
        [HttpPut]
        [ODataRoute("({key})")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(RecipeExpansion), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public IActionResult Put([FromODataUri] long key, [FromBody] Delta<RecipeExpansion> delta)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            RecipeExpansion record = _dbContext.RecipeExpansions.SingleOrDefault(r => r.Id == key);
            if (record == null)
            {
                return NotFound();
            }

            _logger.LogDebug("Updating {id}", record.Id);
            delta.Put(record);
            SaveChanges();
            _logger.LogDebug("Updated {id}", record.Id);

            return Updated(record);
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
        [HttpPatch]
        [ODataRoute("({key})")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(RecipeExpansion), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public IActionResult Patch([FromODataUri] long key, [FromBody] Delta<RecipeExpansion> delta)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var record = _dbContext.RecipeExpansions.SingleOrDefault(r => r.Id == key);
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

        /// <summary>
        /// Cancels a record.
        /// </summary>
        /// <param name="key">The record to cancel.</param>
        /// <param name="suspendOnly">Indicates if the record should only be suspended.</param>
        /// <returns>None</returns>
        /// <response code="404">The record does not exist.</response>
        /// <response code="204">The record was successfully canceled.</response>
        [HttpDelete]
        [ODataRoute("({key})")]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public IActionResult Delete([FromODataUri] long key, bool suspendOnly)
        {
            var record = _dbContext.RecipeExpansions.SingleOrDefault(r => r.Id == key);
            if (record == null)
            {
                return NotFound();
            }

            if (suspendOnly)
            {
                // Chef does not have a disable property.
            } else {
                _logger.LogDebug("Deleting {id}", record.Id);
                _dbContext.RecipeExpansions.Remove(record);
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
