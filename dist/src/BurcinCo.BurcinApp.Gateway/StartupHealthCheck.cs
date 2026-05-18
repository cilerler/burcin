using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BurcinCo.BurcinApp.Gateway;

internal class StartupHealthCheck : IHealthCheck
{
	private volatile bool _isReady;

	public bool StartupCompleted
	{
		get => _isReady;
		set => _isReady = value;
	}

	public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
	{
		if (StartupCompleted)
		{
			return Task.FromResult(HealthCheckResult.Healthy("The startup task has completed."));
		}

		return Task.FromResult(HealthCheckResult.Unhealthy("The startup task is still running."));
	}
}
