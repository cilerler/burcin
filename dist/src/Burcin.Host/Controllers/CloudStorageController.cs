using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using StatusCodes = Microsoft.AspNetCore.Http.StatusCodes;
using Ruya.Services.CloudStorage.Abstractions;

namespace Burcin.Host.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class CloudStorageController : ControllerBase
	{
		private readonly IServiceProvider _serviceProvider;
		private readonly ILogger _logger;
		private readonly ICloudFileService _fileService;

		public CloudStorageController(IServiceProvider serviceProvider, ILogger<CloudStorageController> logger, ICloudFileService fileService)
		{
			_serviceProvider = serviceProvider;
			_logger = logger;
			_fileService = fileService;
		}

		/// <summary>
		/// Retrieves file metadata from the provider
		/// </summary>
		/// <param name="fileName"></param>
		/// <param name="bucketName"></param>
		/// <returns>ICloudFileMetadata</returns>
		/// <response code="200">The record was successfully retrieved.</response>
		/// <response code="404">The record does not exist.</response>
		[Route("[action]")]
		[HttpGet]
		[Produces("application/json")]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		[ProducesResponseType(StatusCodes.Status500InternalServerError)]
		[ProducesResponseType(typeof(ICloudFileMetadata), StatusCodes.Status200OK)]
		public IActionResult GetFileMetadata(string fileName, string bucketName)
		{
			try
			{
				ICloudFileMetadata remoteFile = _fileService.GetFileMetadata(fileName, bucketName);
				return Ok(remoteFile);
			}
			catch (ArgumentException aex) when (aex.Message.StartsWith("Not Found"))
			{
				return NotFound();
			}
		}
	}
}
