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

			// If you reference Microsoft.Extensions.Options use it as below
			// serviceCollection.Configure<HelperSetting>(options=>configuration.GetSection(HelperSetting.ConfigurationSectionName));

			// IF no reference Microsoft.Extensions.Options.ConfigurationExtensions use it as below
			serviceCollection.Configure<HelperSetting>(configuration.GetSection(HelperSetting.ConfigurationSectionName));

	        return serviceCollection.AddTransient<Helper>();
        }
    }
}
