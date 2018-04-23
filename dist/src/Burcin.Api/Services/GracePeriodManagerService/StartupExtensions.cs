using System;
using Burcin.Api.Services.GracePeriodManagerService;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// ReSharper disable once CheckNamespace
namespace Ruya
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

            serviceCollection.Configure<Setting>(configuration.GetSection(Setting.ConfigurationSectionName));
            return serviceCollection.AddSingleton<IHostedService, Service>();
        }
    }
}
