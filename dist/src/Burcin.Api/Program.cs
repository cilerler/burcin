using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Burcin.Api.Middlewares;
using Burcin.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Ruya.Extensions.Logging;
using Ruya.Primitives;
using Serilog;
using StackExchange.Redis;
#if (EntityFramework)
using Microsoft.EntityFrameworkCore;
using Burcin.Data;
#endif

namespace Burcin.Api
{
	public sealed class Program
	{
#pragma warning disable IDE1006 // Naming Styles
		public static async Task Main(string[] args)
#pragma warning restore IDE1006 // Naming Styles
		{
			bool argsRetrievedFromEnvironmentVariable = false;
			if (!args.Any())
			{
				args = EnvironmentHelper.EnvironmentArgs;
				argsRetrievedFromEnvironmentVariable = !args.Any();
			}

			IWebHost host = BuildHost(args);
			ILogger<Program> logger = host.Services.GetService<ILogger<Program>>();
			IWebHostEnvironment hostingEnvironment = host.Services.GetService<IWebHostEnvironment>();
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

		public static IWebHost BuildHost(string[] args)
		{
			string pathToExe = Assembly.GetExecutingAssembly().Location;
			string pathToContentRoot = Path.GetDirectoryName(pathToExe);
			Environment.CurrentDirectory = pathToContentRoot;

			var hostBuilder = new WebHostBuilder();
			if (string.IsNullOrEmpty(hostBuilder.GetSetting(WebHostDefaults.ContentRootKey)))
			{
				hostBuilder.UseContentRoot(Directory.GetCurrentDirectory());
			}

			if (args != null)
			{
				hostBuilder.UseConfiguration(new ConfigurationBuilder().AddCommandLine(args)
																	   .Build());
			}

			hostBuilder.UseKestrel((builderContext, options) => options.Configure(builderContext.Configuration.GetSection("Kestrel")))
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

													  IWebHostEnvironment hostingEnvironment = hostingContext.HostingEnvironment;
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
											 services.PostConfigure((Action<HostFilteringOptions>) (options =>
																									 {
																										 if (options.AllowedHosts != null
																										  && options.AllowedHosts.Count != 0)
																										 {
																											 return;
																										 }

																										 string str = hostingContext.Configuration["AllowedHosts"];
																										 string[] strArray1;
																										 if (str == null)
																										 {
																											 strArray1 = null;
																										 }
																										 else
																										 {
																											 strArray1 = str.Split(new char[1]
																																   {
																																	   ';'
																																   }
																																 , StringSplitOptions.RemoveEmptyEntries);
																										 }

																										 string[] strArray2 = strArray1;
																										 HostFilteringOptions filteringOptions = options;
																										 string[] strArray3;
																										 if (strArray2 == null
																										  || strArray2.Length == 0)
																										 {
																											 strArray3 = new string[1]
																														 {
																															 "*"
																														 };
																										 }
																										 else
																										 {
																											 strArray3 = strArray2;
																										 }

																										 filteringOptions.AllowedHosts = strArray3;
																									 }));
											  services.AddSingleton((IOptionsChangeTokenSource<HostFilteringOptions>) new ConfigurationChangeTokenSource<HostFilteringOptions>(hostingContext.Configuration));
											  services.AddTransient<IStartupFilter, HostFilteringStartupFilter>();

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
										  })
					   .UseIIS()
					   .UseIISIntegration()
					   .UseDefaultServiceProvider((context, options) => options.ValidateScopes = context.HostingEnvironment.IsDevelopment())
						//!++ Following line fixes System.InvalidOperationException: 'Cannot resolve 'Burcin.Domain.Helper' from root provider because it requires scoped service 'Microsoft.Extensions.Options.IOptionsSnapshot`1[Burcin.Domain.HelperSetting]'.' for Development environment
					   .UseDefaultServiceProvider(options => options.ValidateScopes = false)
					   .UseStartup<Startup>()
					   .CaptureStartupErrors(true)
					   .UseSetting("detailedErrors", "true")
					   .UseShutdownTimeout(TimeSpan.FromSeconds(5));
			return hostBuilder.Build();
		}
	}

	internal class HostFilteringStartupFilter : IStartupFilter
	{
		public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
		{
		return (Action<IApplicationBuilder>) (app =>
		{
			app.UseHostFiltering();
			next(app);
		});
		}
	}
}
