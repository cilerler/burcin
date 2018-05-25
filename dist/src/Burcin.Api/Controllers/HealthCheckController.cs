using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.HealthChecks;

namespace Burcin.Api.Controllers
{
    [Route("api/[controller]")]
    public class HealthCheckController : Controller
    {
        private readonly IHealthCheckService _healthCheck;

        public HealthCheckController(IHealthCheckService healthCheck)
        {
            _healthCheck = healthCheck;
        }

        [HttpGet]
        public async Task<CompositeHealthCheckResult> Get() => await _healthCheck.CheckHealthAsync();
        //var timedTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        //var checkHealth= await _healthCheck.CheckHealthAsync(timedTokenSource.Token);
        //return checkHealth;
    }
}
