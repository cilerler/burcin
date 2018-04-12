using Burcin.Domain;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.Extensions.Logging;
using Ruya.Primitives;
using Serilog;

namespace Burcin.Api
{
    public class Program
    {
        private static readonly TimeSpan HealthCheckTimeout = TimeSpan.FromSeconds(15);
        internal static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromSeconds(5);

        private static readonly Assembly ExecutingAssembly = Assembly.GetExecutingAssembly();

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                    //!++ Following line fixes System.InvalidOperationException: 'Cannot resolve 'Burcin.Domain.Helper' from root provider because it requires scoped service 'Microsoft.Extensions.Options.IOptionsSnapshot`1[Burcin.Domain.HelperSetting]'.' for Development environment
                   .UseDefaultServiceProvider(options => options.ValidateScopes = false)
                   .CaptureStartupErrors(true)
                   .UseSetting("detailedErrors", "true")
                   .UseApplicationInsights()
                   //.UseHealthChecks(911, HealthCheckTimeout)
                   .UseHealthChecks("/health", HealthCheckTimeout)
                   .UseStartup<Startup>()
                   .UseSerilog()
                   .Build();

        public static void Main(string[] args)
        {
            IWebHost host = BuildWebHost(args);
            IServiceProvider serviceProvider = host.Services;
            RunProgram(serviceProvider, args, host);
            Thread.Sleep(TimeSpan.FromSeconds(5));
        }

        public static void RegisterExternalServices(IServiceCollection serviceCollection, IConfiguration configuration)
        {
            //! Uncomment line below if you are using EntityFramework
            //DbContextFactory.RegisterExternalServices(serviceCollection, configuration);
            serviceCollection.Configure<HelperSetting>(configuration.GetSection(HelperSetting.ConfigurationSectionName));
            serviceCollection.AddTransient<Helper>();
        }

        private static void RunProgram(IServiceProvider serviceProvider, IEnumerable<string> args, IWebHost host)
        {
            var logger = serviceProvider.GetService<ILogger<Program>>();
            AssemblyName assemblyName = ExecutingAssembly.GetName();
            string assemblyInfo = $"{assemblyName.Name} v{assemblyName.Version}";
            Guid applicationGuid = Guid.NewGuid();
            using (Ruya.Extensions.Logging.LoggerExtensionsHelper.ProgramScope(logger, assemblyInfo, applicationGuid.ToString()))
            {
                Ruya.Extensions.Logging.LoggerExtensionsHelper.ProgramStarted(logger
                                                                            , EnvironmentHelper.EnvironmentName
                                                                            , Environment.UserInteractive
                                                                            , Debugger.IsAttached
                                                                            , Process.GetCurrentProcess().Id
                                                                            , Thread.CurrentThread.ManagedThreadId
                                                                            , args.ToArray());

                Initialize(serviceProvider);
                host.Run();

                Ruya.Extensions.Logging.LoggerExtensionsHelper.ProgramStopping(logger
                                                                             , Process.GetCurrentProcess().Id
                                                                             , Thread.CurrentThread.ManagedThreadId);
            }
        }

        private static void Initialize(IServiceProvider serviceProvider)
        {
            var helper = serviceProvider.GetService<Helper>();
            helper.Echo("Hello World!");
        }
    }
}
