using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Burcin.Console.Services.GracePeriodManagerService
{
    public class Service : BackgroundService
    {
        private readonly ILogger<Service> _logger;
        private readonly Setting _options;

        // ReSharper disable once SuggestBaseTypeForParameter
        public Service(ILogger<Service> logger, IOptionsMonitor<Setting> options)
        {
            _logger = logger;
            _options = options.CurrentValue;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            cancellationToken.Register(() => _logger.LogDebug("Cancellation request received for background task"));

            while (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("Service is calling task");
                DoWork(cancellationToken);
                DateTime nextOccurence = DateTime.Parse(_options.DelayTime);
                DateTime now = DateTime.Now;
                TimeSpan delay = nextOccurence - now;
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
        }
    }
}
