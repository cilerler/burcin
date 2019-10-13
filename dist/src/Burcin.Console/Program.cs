using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Ruya.AppDomain;
using Ruya.Extensions.Logging;
using Ruya.Primitives;
using Burcin.Domain;
using Microsoft.Extensions.Options;
#if (EntityFramework)
using Microsoft.EntityFrameworkCore;
using Burcin.Data;
#endif

namespace Burcin.Console
{
	public class StartTimeHeader
	{
		public const string InMemoryCacheKey = "serverStartTime";
		public const string DistributedCacheKey = "lastServerStartTime";
	}

	public class Program
	{
#pragma warning disable IDE1006 // Naming Styles
		public static async Task Main(string[] args)
#pragma warning restore IDE1006 // Naming Styles
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
					Trace.WriteLine(message);
					return;
				}

				bool argsRetrievedFromEnvironmentVariable = false;
				if (!args.Any())
				{
					args = EnvironmentHelper.EnvironmentArgs;
					argsRetrievedFromEnvironmentVariable = !args.Any();
				}

				IHost host = BuildHost(args);
				ILogger<Program> logger = host.Services.GetService<ILogger<Program>>();
				IHostEnvironment hostingEnvironment = host.Services.GetService<IHostEnvironment>();
				EnvironmentHelper.EnvironmentName = hostingEnvironment.EnvironmentName;

				AssemblyName assemblyName = Assembly.GetExecutingAssembly()
				                                    .GetName();
				var applicationGuid = Guid.NewGuid();

				using (logger.ProgramScope(assemblyName.Name
				                         , assemblyName.Version.ToString()
				                         , applicationGuid.ToString()))
				{
					logger.ProgramStarted(Process.GetCurrentProcess().Id
					                    , Thread.CurrentThread.ManagedThreadId);

					logger.ProgramInitial(EnvironmentHelper.EnvironmentName
					                    , EnvironmentHelper.IsDocker
					                    , Environment.UserInteractive
					                    , Debugger.IsAttached
					                    , argsRetrievedFromEnvironmentVariable
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
			serviceProvider.GetRequiredService<IHostApplicationLifetime>()
			               .ApplicationStopped.Register(Log.CloseAndFlush);

			ILogger<Program> logger = serviceProvider.GetService<ILogger<Program>>();

			DateTimeOffset serverStartTime = DateTime.UtcNow;

			MemoryCacheEntryOptions memoryCacheEntryOptions = new MemoryCacheEntryOptions().SetPriority(CacheItemPriority.NeverRemove);
			IMemoryCache memoryCache = serviceProvider.GetRequiredService<IMemoryCache>();
			memoryCache.Set(StartTimeHeader.InMemoryCacheKey
			              , serverStartTime
			              , memoryCacheEntryOptions);

			byte[] value = Encoding.UTF8.GetBytes(serverStartTime.ToString("s"));
			DistributedCacheEntryOptions cacheEntryOptions = new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(30));

			try
			{
				IDistributedCache distributedCache = serviceProvider.GetRequiredService<IDistributedCache>();
				distributedCache.Set(StartTimeHeader.DistributedCacheKey, value, cacheEntryOptions);
			}
			catch (Exception e) when (e.Source.Equals("StackExchange.Redis"))
			{
				logger.LogWarning(e, e.Message);
			}
			catch (Exception e)
			{
				logger.LogError(e, e.Message);
			}

			Helper helper = serviceProvider.GetService<Helper>();
			helper.Echo("Hello World!");
		}

		public static IHost BuildHost(string[] args)
		{
			string pathToExe;
			#if (WindowsService)
			bool isService = !Debugger.IsAttached && !args.Contains("--console");
			pathToExe = Process.GetCurrentProcess().MainModule.FileName;
			#else
			pathToExe = Assembly.GetExecutingAssembly().Location;
			#endif
			string pathToContentRoot = Path.GetDirectoryName(pathToExe);
			Environment.CurrentDirectory = pathToContentRoot;

			IHostBuilder hostBuilder = new HostBuilder();
			hostBuilder.UseContentRoot(pathToContentRoot)
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
			           .ConfigureAppConfiguration((hostingContext, appConfig) =>
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

                                                      IHostEnvironment hostingEnvironment = hostingContext.HostingEnvironment;
				                                      appConfig.AddJsonFile(configurationFileName
				                                                          , true
				                                                          , true);
				                                      appConfig.AddJsonFile($"{configurationFileNameWithoutExtension}.{hostingEnvironment.EnvironmentName}{configurationExtension}"
				                                                          , true
				                                                          , true);

				                                      if (hostingEnvironment.IsDevelopment())
				                                      {
					                                      var assembly = Assembly.Load(new AssemblyName(hostingEnvironment.ApplicationName));
					                                      if (assembly != null)
					                                      {
						                                      //x appConfig.AddUserSecrets<Startup>();
						                                      appConfig.AddUserSecrets(assembly
						                                                             , true);
					                                      }
				                                      }

				                                      appConfig.AddEnvironmentVariables(prefix: "ASPNETCORE_");
				                                      if (args != null)
				                                      {
					                                      appConfig.AddCommandLine(args);
				                                      }
			                                      })
			           .ConfigureLogging((hostingContext, loggingBuilder) =>
			                             {
				                             loggingBuilder.AddConfiguration(hostingContext.Configuration.GetSection("Logging"))
															//.AddConsole()
				                                            //.AddDebug()
															//.AddEventSourceLogger();
				                                           .AddSerilog(dispose: true);
			                             })
			           .ConfigureServices((hostingContext, services) =>
			                              {
				                              services.AddLogging();
				                              Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(hostingContext.Configuration)
				                                                                    .CreateLogger();

				                              services.AddOptions();

				                              services.AddMemoryCache();
				                              #if (!CacheExists)
				                              services.AddDistributedMemoryCache();
				                              #endif

#if (CacheSqlServer)
											  services.AddDistributedSqlServerCache(options =>
																						{
																							options.ConnectionString = hostingContext.Configuration.GetConnectionString(hostingContext.Configuration.GetValue<string>("Cache:SqlServer:ConnectionStringKey"));
																							options.SchemaName = hostingContext.Configuration.GetValue<string>("Cache:SqlServer:SchemaName");
																							options.TableName = hostingContext.Configuration.GetValue<string>("Cache:SqlServer:TableName");
																						});
#endif
#if (CacheRedis)
											  services.AddStackExchangeRedisCache(options =>
																					  {
																						  options.Configuration = hostingContext.Configuration.GetConnectionString(hostingContext.Configuration.GetValue<string>("Cache:Redis:ConnectionStringKey"));
																						  options.InstanceName = hostingContext.Configuration.GetValue<string>("Cache:Redis:InstanceName");
																						  //x hostingContext.Configuration.GetSection("Cache:Redis").Bind(options);
																						  options.ConfigurationOptions =
																							  new ConfigurationOptions
																							  {
																								  AbortOnConnectFail = true
																							  };
																					  });
#endif

#if (EntityFramework)
				                              const string databaseConnectionString = "MsSqlConnection";
				                              string connectionString = hostingContext.Configuration.GetConnectionString(databaseConnectionString);
				                              string assemblyName = hostingContext.Configuration.GetValue(typeof(string)
				                                                                                        , DbContextFactory.MigrationAssemblyNameConfiguration)
				                                                                  .ToString();
				                              services.AddDbContext<BurcinDatabaseDbContext>(options => options.UseSqlServer(connectionString
				                                                                                                   , sqlServerOptions =>
				                                                                                                     {
					                                                                                                     sqlServerOptions.MigrationsAssembly(assemblyName);
					                                                                                                     sqlServerOptions.EnableRetryOnFailure(5
					                                                                                                                                         , TimeSpan.FromSeconds(30)
					                                                                                                                                         , null);
				                                                                                                     }));
				                              #endif

				                              #if (BackgroundService)
				                              services.AddGracePeriodManagerService(hostingContext.Configuration);
				                              #endif

				                              services.AddHelper(hostingContext.Configuration);
			                              });
			#if (WindowsService)
			if (isService)
			{
				hostBuilder.UseServiceBaseLifetime();
			}
			else
			{
			#endif
				hostBuilder.UseConsoleLifetime();
			#if (WindowsService)
			}
			#endif
			return hostBuilder.Build();
		}
	}
}
