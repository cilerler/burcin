using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Burcin.Host.HealthChecks
{
	public class SlowDependencyHealthCheck : IHealthCheck
	{
		public static readonly string HealthCheckName = "slow_dependency";

		private readonly Task _task;

		public SlowDependencyHealthCheck()
		{
			_task = Task.Delay(TimeSpan.FromSeconds(15));
		}

		public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
		{
			if (_task.IsCompleted)
			{
				return Task.FromResult(HealthCheckResult.Healthy("Dependency is ready"));
			}

			return Task.FromResult(new HealthCheckResult(status: context.Registration.FailureStatus
			                                           , description: "Dependency is still initializing"));
		}
	}
}
