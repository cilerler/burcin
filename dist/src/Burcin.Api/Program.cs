using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Burcin.Api.Middlewares;
using Burcin.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ruya.Extensions.Logging;
using Ruya.Primitives;
using Serilog;
#if (EntityFramework)
using Microsoft.EntityFrameworkCore;
using Burcin.Data;

#endif

namespace Burcin.Api
{
	public class Program
	{
		#if (EntityFramework)
		public const string DatabaseConnectionString = "DefaultConnection";
		#endif

		private static readonly TimeSpan HealthCheckTimeout = TimeSpan.FromSeconds(15);
		internal static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromSeconds(5);

		public static void Main(string[] args)
		{
			var argsRetrievedFromEnvironmentVariable = false;
			if (!args.Any())
			{
				args = EnvironmentHelper.EnvironmentArgs;
				argsRetrievedFromEnvironmentVariable = !args.Any();
			}

			IWebHost host = BuildHost(args);
			var logger = host.Services.GetService<ILogger<Program>>();
			var hostingEnvironment = host.Services.GetService<IHostingEnvironment>();
			EnvironmentHelper.EnvironmentName = hostingEnvironment.EnvironmentName;

			AssemblyName assemblyName = Assembly.GetExecutingAssembly()
			                                    .GetName();
			Guid applicationGuid = Guid.NewGuid();

			using (logger.ProgramScope(assemblyName.Name
			                         , assemblyName.Version.ToString()
			                         , applicationGuid.ToString()))
			{
				logger.ProgramStarted(Process.GetCurrentProcess()
				                             .Id
				                    , Thread.CurrentThread.ManagedThreadId);

				logger.ProgramInitial(EnvironmentHelper.EnvironmentName
				                    , EnvironmentHelper.IsDocker
				                    , Environment.UserInteractive
				                    , Debugger.IsAttached
				                    , argsRetrievedFromEnvironmentVariable
				                    , args.ToArray());

				Initialize(host.Services);

				host.Run();

				logger.ProgramStopping(Process.GetCurrentProcess()
				                              .Id
				                     , Thread.CurrentThread.ManagedThreadId);
			}
		}

		private static void Initialize(IServiceProvider serviceProvider)
		{
			// Waiting for https://github.com/serilog/serilog-aspnetcore/issues/49
			serviceProvider.GetRequiredService<IApplicationLifetime>()
			               .ApplicationStopped.Register(Log.CloseAndFlush);

			DateTimeOffset serverStartTime = DateTime.UtcNow;

			MemoryCacheEntryOptions memoryCacheEntryOptions = new MemoryCacheEntryOptions().SetPriority(CacheItemPriority.NeverRemove);
			var memoryCache = serviceProvider.GetRequiredService<IMemoryCache>();
			memoryCache.Set(StartTimeHeader.InMemoryCacheKey
			              , serverStartTime
			              , memoryCacheEntryOptions);

			byte[] value = Encoding.UTF8.GetBytes(serverStartTime.ToString("s"));
			DistributedCacheEntryOptions cacheEntryOptions = new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(30));
			var distributedCache = serviceProvider.GetService<IDistributedCache>();
			distributedCache.Set(StartTimeHeader.DistributedCacheKey
			                   , value
			                   , cacheEntryOptions);

			var helper = serviceProvider.GetService<Helper>();
			helper.Echo("Hello World!");
		}

		public static IWebHost BuildHost(string[] args)
		{
			var hostBuilder = new WebHostBuilder();
			hostBuilder.UseKestrel()
			           .UseContentRoot(Directory.GetCurrentDirectory())
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
				                              Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(hostContext.Configuration)
				                                                                    .CreateLogger();
				                              //x services.AddSingleton(Log.Logger);

				                              services.AddOptions();

				                              services.AddMemoryCache();
				                              #if (!CacheExists)
				                              services.AddDistributedMemoryCache();
				                              #endif

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
				                                           .AddSerilog(
				                                                       //x loggingBuilder.Services.BuildServiceProvider().GetRequiredService<Serilog.ILogger>(),
				                                                       dispose: true);
			                             })
			           .UseIISIntegration()
			           .UseDefaultServiceProvider((context, options) => options.ValidateScopes = context.HostingEnvironment.IsDevelopment())
			            //!++ Following line fixes System.InvalidOperationException: 'Cannot resolve 'Burcin.Domain.Helper' from root provider because it requires scoped service 'Microsoft.Extensions.Options.IOptionsSnapshot`1[Burcin.Domain.HelperSetting]'.' for Development environment
			           .UseDefaultServiceProvider(options => options.ValidateScopes = false)
			           .CaptureStartupErrors(true)
			           .UseSetting("detailedErrors"
			                     , "true")
			           .UseApplicationInsights()
			            #if (HealthChecks)
			            //.UseHealthChecks(911, HealthCheckTimeout)
			           .UseHealthChecks("/health"
			                          , HealthCheckTimeout)
			            #endif
			           .UseStartup<Startup>()
			            //x .UseSerilog(dispose: true)
			           .UseShutdownTimeout(TimeSpan.FromSeconds(5));
			return hostBuilder.Build();
		}
	}
}
