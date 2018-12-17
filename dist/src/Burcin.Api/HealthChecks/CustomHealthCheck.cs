using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Burcin.Api.HealthChecks
{
	public class CustomHealthCheck : IHealthCheck
	{
		public static readonly string HealthCheckName = "custom";

		private readonly IServiceProvider _serviceProvider;

		public CustomHealthCheck(IServiceProvider serviceProvider)
		{
			_serviceProvider = serviceProvider;
		}

		public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken())
		{
			bool conditionHealthy = DateTime.UtcNow.Minute % 2 == 0;
			return Task.FromResult(conditionHealthy
				                       ? HealthCheckResult.Unhealthy(description: "Minute is not even")
				                       : HealthCheckResult.Healthy());
		}
	}
}
