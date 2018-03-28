using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ruya.AppDomain;
using Ruya.Extensions.DependencyInjection;
using Ruya.Primitives;
using Burcin.Domain;

namespace Burcin.Console
{
    public class Program
    {
        private static readonly Assembly ExecutingAssembly = Assembly.GetExecutingAssembly();

        public static void Main(string[] args)
        {
            //x SharpPad.Output.RedirectConsoleOutput(true);
            //x SharpPad.Output.Clear();
            using (var mutex = new Mutex(false, ExecutingAssembly.GetName().Name))
            {
                const int mutexTimeout = 2;
                if (!mutex.WaitOne(TimeSpan.FromSeconds(mutexTimeout), false))
                {
                    const string message = "Another instance is running.";
                    Debug.WriteLine(message);
                    return;
                }

                #region Enter
#if DEBUG
                const string environmentName = Constants.Development;
#else
                const string environmentName = Constants.Production;
#endif
                StartupInjector.Instance.EnvironmentName = environmentName;
                StartupInjector.Instance.RegisterExternalServices = RegisterExternalServices;
                SerilogHelper.Register(Startup.Instance.Configuration);
                var unhandledExceptionHelper = new UnhandledExceptionHelper();
                unhandledExceptionHelper.Register();
                unhandledExceptionHelper.SetLogger(Startup.Instance.ServiceProvider.GetService<ILoggerFactory>());
                unhandledExceptionHelper.LogExistingCrashes(true);

                #endregion

                RunProgram(Startup.Instance.ServiceProvider, args);

                #region Exit

                unhandledExceptionHelper.Unregister();
                //x Log.CloseAndFlush();
                Thread.Sleep(TimeSpan.FromSeconds(5));

                #endregion
            }
        }

        public static void RegisterExternalServices(IServiceCollection serviceCollection, IConfigurationRoot configuration)
        {
            //! Uncomment line below if you are using EntityFramework
            //DbContextFactory.RegisterExternalServices(serviceCollection, configuration);
            serviceCollection.Configure<HelperSetting>(configuration.GetSection(HelperSetting.ConfigurationSectionName));
            serviceCollection.AddTransient<Helper>();
        }

        private static void RunProgram(IServiceProvider serviceProvider, IEnumerable<string> args)
        {
            var logger = serviceProvider.GetService<ILogger<Program>>();
            AssemblyName assemblyName = ExecutingAssembly.GetName();
            string assemblyInfo = $"{assemblyName.Name} v{assemblyName.Version}";
            Guid applicationGuid = Guid.NewGuid();
            using (Ruya.Extensions.Logging.LoggerExtensionsHelper.ProgramScope(logger, assemblyInfo, applicationGuid.ToString()))
            {
                Ruya.Extensions.Logging.LoggerExtensionsHelper.ProgramStarted(logger
                                                                            , EnvironmentHelper.Name
                                                                            , Environment.UserInteractive
                                                                            , Debugger.IsAttached
                                                                            , Process.GetCurrentProcess().Id
                                                                            , Thread.CurrentThread.ManagedThreadId
                                                                            , args.ToArray());

                var helper = serviceProvider.GetService<Helper>();
                string output = helper.Echo("Hello World!");
                logger.LogInformation(output);

                Ruya.Extensions.Logging.LoggerExtensionsHelper.ProgramStopping(logger
                                                                             , Process.GetCurrentProcess().Id
                                                                             , Thread.CurrentThread.ManagedThreadId);
            }
        }
    }
}
