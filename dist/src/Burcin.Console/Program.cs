using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Burcin.Data;
using Burcin.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ruya.AppDomain;
using Ruya.Extensions.Logging;
using Ruya.Primitives;
using Serilog;

namespace Burcin.Console
{
	public class StartTimeHeader
	{
		public const string InMemoryCacheKey = "serverStartTime";
		public const string DistributedCacheKey = "lastServerStartTime";
	}

	public class Program
	{
		#if (EntityFramework)
		public const string DatabaseConnectionString = "DefaultConnection";
		#endif

		public static async Task Main(string[] args)
		{
			var unhandledExceptionHelper = new UnhandledExceptionHelper();
			bool enableUnhandledExceptionHelper = !Debugger.IsAttached;
			if (enableUnhandledExceptionHelper)
			{
				unhandledExceptionHelper.Register();
				//x unhandledExceptionHelper.SetLogger(Startup.Instance.ServiceProvider.GetService<ILoggerFactory>());
				//x unhandledExceptionHelper.LogExistingCrashes(true);
			}

			using (var mutex = new Mutex(false
			                           , Assembly.GetExecutingAssembly()
			                                     .GetName()
			                                     .Name))
			{
				const int mutexTimeout = 2;
				if (!mutex.WaitOne(TimeSpan.FromSeconds(mutexTimeout)
				                 , false))
				{
					const string message = "Another instance is running.";
					Debug.WriteLine(message);
					return;
				}

				IHost host = BuildHost(args);
				var logger = host.Services.GetService<ILogger<Program>>();
				AssemblyName assemblyName = Assembly.GetExecutingAssembly()
				                                    .GetName();
				string assemblyInfo = $"{assemblyName.Name} v{assemblyName.Version}";
				Guid applicationGuid = Guid.NewGuid();

				//using (logger.BeginScope("{ApplicationName} :: {ApplicationVersion} :: {ApplicationGuid}", assemblyName.Name, assemblyName.Version, applicationGuid))
				//logger.LogInformation($"{new string('-', 19)} Start {nameof(RunProgram)} Environment.UserInteractive {{Environment_UserInteractive}}, Debugger.IsAttached {{Debugger_IsAttached}}, {{ProcessId}}, {{ThreadId}}, Arguments {{Args}}", Environment.UserInteractive, Debugger.IsAttached, Process.GetCurrentProcess().Id, Thread.CurrentThread.ManagedThreadId, args);
				//logger.LogInformation($"{new string('-', 19)} Stop {nameof(RunProgram)} {{ProcessId}}, {{ThreadId}}", Process.GetCurrentProcess().Id, Thread.CurrentThread.ManagedThreadId);

				using (logger.ProgramScope(assemblyInfo
				                         , applicationGuid.ToString()))
				{
					logger.ProgramStarted(EnvironmentHelper.EnvironmentName
					                    , Environment.UserInteractive
					                    , Debugger.IsAttached
					                    , Process.GetCurrentProcess()
					                             .Id
					                    , Thread.CurrentThread.ManagedThreadId
					                    , args.ToArray());

					Initialize(host.Services);
					await host.RunAsync();

					logger.ProgramStopping(Process.GetCurrentProcess()
					                              .Id
					                     , Thread.CurrentThread.ManagedThreadId);
				}
			}

			if (enableUnhandledExceptionHelper)
			{
				unhandledExceptionHelper.Unregister();
			}
		}

		private static void Initialize(IServiceProvider serviceProvider)
		{
			// Waiting for https://github.com/serilog/serilog-aspnetcore/issues/49
			serviceProvider.GetRequiredService<IApplicationLifetime>().ApplicationStopped.Register(Log.CloseAndFlush);

			DateTimeOffset serverStartTime = DateTime.UtcNow;

			MemoryCacheEntryOptions memoryCacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(30));
			var memoryCache = serviceProvider.GetService<IMemoryCache>();
			memoryCache.Set(StartTimeHeader.InMemoryCacheKey
			              , serverStartTime
			              , memoryCacheEntryOptions);

			var value = Encoding.UTF8.GetBytes(serverStartTime.ToString("s"));
			DistributedCacheEntryOptions cacheEntryOptions = new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(30));
			var distributedcache = serviceProvider.GetService<IDistributedCache>();
			distributedcache.Set(StartTimeHeader.DistributedCacheKey
			                   , value
			                   , cacheEntryOptions);

			var helper = serviceProvider.GetService<Helper>();
			helper.Echo("Hello World!");
		}

		public static IHost BuildHost(string[] args) =>
			new HostBuilder()
				//x .UseContentRoot(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName))
			.ConfigureHostConfiguration(hostConfig =>
			                            {
				                            hostConfig.AddJsonFile("hostsettings.json"
				                                                 , false
				                                                 , true);
				                            hostConfig.AddEnvironmentVariables();
				                            if (args != null)
				                            {
					                            hostConfig.AddCommandLine(args);
				                            }
			                            })
			.ConfigureAppConfiguration((hostContext, appConfig) =>
			                           {
				                           appConfig.SetBasePath(Environment.CurrentDirectory);
				                           appConfig.AddInMemoryCollection(new Dictionary<string, string>());

				                           const string configurationExtension = ".json";
				                           const string configurationFileNameWithoutExtension = "appsettings";
				                           string configurationFileName = $"{configurationFileNameWithoutExtension}{configurationExtension}";
				                           if (!File.Exists(configurationFileName))
				                           {
					                           throw new ArgumentException($"Configuration file does not exist!  Current Directory {Directory.GetCurrentDirectory()}");
				                           }

				                           appConfig.AddJsonFile(configurationFileName
				                                               , false
				                                               , true);
				                           appConfig.AddJsonFile($"{configurationFileNameWithoutExtension}.{hostContext.HostingEnvironment.EnvironmentName}{configurationExtension}"
				                                               , true
				                                               , true);

				                           if (hostContext.HostingEnvironment.IsDevelopment())
				                           {
					                           Assembly assembly = Assembly.Load(new AssemblyName(hostContext.HostingEnvironment.ApplicationName));
					                           if (assembly != null)
					                           {
						                           //x appConfig.AddUserSecrets<Startup>();
						                           appConfig.AddUserSecrets(assembly
						                                                  , true);
					                           }
				                           }

				                           appConfig.AddEnvironmentVariables();
				                           if (args != null)
				                           {
					                           appConfig.AddCommandLine(args);
				                           }
			                           })
			.ConfigureServices((hostContext, services) =>
			                   {
				                   services.AddLogging();
				                   Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(hostContext.Configuration).CreateLogger();
								   services.AddSingleton(Log.Logger);

								   services.AddOptions();

				                   services.AddMemoryCache();
				                   services.AddDistributedMemoryCache();

				                   #if (CacheSqlServer)
				                   services.AddDistributedSqlServerCache(options =>
				                                                         {
					                                                         options.ConnectionString = hostContext.Configuration.GetConnectionString(hostContext.Configuration.GetValue<string>("Cache:SqlServer:ConnectionStringKey"));
					                                                         options.SchemaName = hostContext.Configuration.GetValue<string>("Cache:SqlServer:SchemaName");
					                                                         options.TableName = hostContext.Configuration.GetValue<string>("Cache:SqlServer:TableName");
				                                                         });
				                   #endif
				                   #if (CacheRedis)
				                   services.AddDistributedRedisCache(options =>
				                                                     {
					                                                     options.Configuration = hostContext.Configuration.GetConnectionString(hostContext.Configuration.GetValue<string>("Cache:Redis:ConnectionStringKey"));
					                                                     options.InstanceName = hostContext.Configuration.GetValue<string>("Cache:Redis:InstanceName");
					                                                     //x hostContext.Configuration.GetSection("Cache:Redis").Bind(options);
				                                                     });
				                   #endif

				                   #if (EntityFramework)
				                   const string databaseConnectionString = "DefaultConnection";
				                   string connectionString = hostContext.Configuration.GetConnectionString(databaseConnectionString);
				                   string assemblyName = hostContext.Configuration.GetValue(typeof(string)
				                                                                          , DbContextFactory.MigrationAssemblyNameConfiguration)
				                                                    .ToString();
				                   services.AddDbContext<BurcinDbContext>(options => options.UseSqlServer(connectionString
				                                                                                        , sqlServerOptions =>
				                                                                                          {
					                                                                                          sqlServerOptions.MigrationsAssembly(assemblyName);
					                                                                                          sqlServerOptions.EnableRetryOnFailure(5
					                                                                                                                              , TimeSpan.FromSeconds(30)
					                                                                                                                              , null);
				                                                                                          }));
				                   #endif

				                   #if (BackgroundService)
                                   services.AddGracePeriodManagerService(hostContext.Configuration);
                                   #endif

				                   services.AddHelper(hostContext.Configuration);
			                   })
			.ConfigureLogging((hostContext, loggingBuilder) =>
			                  {
				                  loggingBuilder.AddConfiguration(hostContext.Configuration.GetSection("Logging"))
				                                //.AddDebug()
				                                //.AddConsole()
				                                .AddSerilog(loggingBuilder.Services.BuildServiceProvider()
				                                                          .GetRequiredService<Serilog.ILogger>()
				                                          , dispose: true);
			                  })
			.UseConsoleLifetime()
			.Build();
	}
}
