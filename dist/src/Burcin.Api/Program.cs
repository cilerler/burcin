using Burcin.Domain;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Burcin.Api
{
    public class Program
    {
        public static void Main(string[] args) => BuildWebHost(args).Run();

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseApplicationInsights()
                .UseStartup<Startup>()
                .Build();


        public static void RegisterExternalServices(IServiceCollection serviceCollection, IConfigurationRoot configuration)
        {
            //! Uncomment line below if you are using EntityFramework
            //DbContextFactory.RegisterExternalServices(serviceCollection, configuration);
            serviceCollection.Configure<HelperSetting>(configuration.GetSection(HelperSetting.ConfigurationSectionName));
            serviceCollection.AddTransient<Helper>();
        }
    }
}
