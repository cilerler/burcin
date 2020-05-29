using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Burcin.Host.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class HealthCheckController : ControllerBase
	{
		private readonly HealthCheckService _healthCheck;

		public HealthCheckController(HealthCheckService healthCheck)
		{
			_healthCheck = healthCheck;
		}

		[HttpGet]
		public async Task<HealthReport> Get()
		{
			return await _healthCheck.CheckHealthAsync();
			//var timedTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
			//HealthReport checkHealth = await _healthCheck.CheckHealthAsync(timedTokenSource.Token);
			//return checkHealth;
		}
	}
}
