using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Burcin.Domain
{
    public static class StartupExtensions
    {
        public static IServiceCollection AddHelper(this IServiceCollection serviceCollection, IConfiguration configuration)
        {
            if (serviceCollection == null)
            {
                throw new ArgumentNullException(nameof(serviceCollection));
            }
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            serviceCollection.Configure<HelperSetting>(options => configuration.GetSection(HelperSetting.ConfigurationSectionName));
            return serviceCollection.AddTransient<Helper>();
        }
    }
}
