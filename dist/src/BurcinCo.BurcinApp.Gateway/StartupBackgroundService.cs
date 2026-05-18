using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BurcinCo.BurcinApp.Gateway;

internal class StartupBackgroundService : BackgroundService
{
	private static readonly EventId StartupBackgroundServiceStarting = new(1100, nameof(StartupBackgroundServiceStarting));
	private static readonly EventId StartupBackgroundServiceCompleted = new(1101, nameof(StartupBackgroundServiceCompleted));

	private readonly ILogger _logger;
	private readonly StartupHealthCheck _healthCheck;

	public StartupBackgroundService(ILogger<StartupBackgroundService> logger, StartupHealthCheck healthCheck)
	{
		_logger = logger;
		_healthCheck = healthCheck;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.Log(LogLevel.Information, StartupBackgroundServiceStarting, "Executing startup background service.");

		// Simulate the effect of a long-running task.
		await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);

		_logger.Log(LogLevel.Information, StartupBackgroundServiceCompleted, "Startup background service completed.");

		_healthCheck.StartupCompleted = true;
	}
}
