using System;
using Burcin.Console.Services.GracePeriodManagerService;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// ReSharper disable once CheckNamespace
namespace Burcin
{
    public static partial class StartupExtensions
    {
        public static IServiceCollection AddGracePeriodManagerService(this IServiceCollection serviceCollection, IConfiguration configuration)
        {
            if (serviceCollection == null)
            {
                throw new ArgumentNullException(nameof(serviceCollection));
            }

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

	        serviceCollection.AddOptions<Setting>()
	                         .Bind(configuration.GetSection(Setting.ConfigurationSectionName))
	                         .ValidateDataAnnotations()
							 .Validate(d =>
									   {
										   if (!d.IsEnabled)
										   {
											   return true;
										   }

										   if (d.NextOccurence < DateTime.Now)
										   {
											   return false;
										   }

										   TimeSpan difference = d.NextOccurence - DateTime.Now;
										   return difference.TotalHours < 1;
									   }
									 , "Update the crontab settings of the `appsettings.json` file to `0 * * * 0-6`")
				;
	        return serviceCollection.AddHostedService<Service>();
        }
    }
}
