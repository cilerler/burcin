using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Burcin.Api.Services.GracePeriodManagerService
{
    public class Service : BackgroundService
    {
        private readonly ILogger<Service> _logger;
        private readonly Setting _options;

        // ReSharper disable once SuggestBaseTypeForParameter
        public Service(ILogger<Service> logger, IOptions<Setting> options)
        {
            _logger = logger;
	        try
	        {
		        _options = options.Value;
			}
			catch (OptionsValidationException ex)
	        {
		        _logger.LogError(ex.Failures.First());
				throw;
	        }
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
			cancellationToken.Register(() => _logger.LogDebug("Cancellation request received for background task"));
	        if (!_options.IsEnabled)
	        {
		        await StopAsync(cancellationToken);
				return;
	        }
			while (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("Service is calling task");
                DoWork(cancellationToken);
                TimeSpan delay = _options.NextOccurence - DateTime.Now;
                _logger.LogTrace("Delay before call {DelayTime}", delay);
                await Task.Delay(delay
                               , cancellationToken);
            }
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Service is starting.");
            await base.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Service is stopping.");
            await base.StopAsync(cancellationToken);
        }

        private void DoWork(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Task is running");
			var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
            using (var scope = scopeFactory.CreateScope())
            using (var context = scope.ServiceProvider.GetRequiredService<Helper>())
            {
				context.Echo("Hello World from the Background Service!");
			}
        }
    }
}
