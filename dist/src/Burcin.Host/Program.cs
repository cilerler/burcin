using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#if (WebApplicationExists)
using Burcin.Host.Middlewares;
#endif
using Burcin.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Ruya.AppDomain;
using Ruya.Extensions.Logging;
using Ruya.Primitives;
#if (SerilogSupport)
using Serilog;
using Serilog.Exceptions;
using Serilog.Exceptions.Core;
using Serilog.Exceptions.Destructurers;
using Serilog.Exceptions.EntityFrameworkCore.Destructurers;
using Serilog.Exceptions.SqlServer.Destructurers;
#endif
#if (CacheRedis)
using Microsoft.Extensions.Caching.StackExchangeRedis;
using StackExchange.Redis;
#endif
#if (EntityFramework)
using Microsoft.EntityFrameworkCore;
using Burcin.Data;
#endif

namespace Burcin.Host
{
	public sealed class Program
	{
#pragma warning disable IDE1006 // Naming Styles
		public static async Task Main(string[] args)
#pragma warning restore IDE1006 // Naming Styles
		{
			Console.WriteLine($"BUILD-TIME\n{new string('=', 20)}\n{await File.ReadAllTextAsync(@"Resources/BuildInfo.txt")}\nRUN-TIME\n{new string('=', 20)}\n{Assembly.GetExecutingAssembly().GetName().Version}\nDOTNET_ENVIRONMENT: {Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")}\nDOTNET_RUNNING_IN_CONTAINER: {Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER")}\nMACHINENAME: {Environment.MachineName}");

#if (ConsoleApplication)
			bool isConsole = args.Contains("--console");
			if (isConsole)
			{
				var unhandledExceptionHelper = new UnhandledExceptionHelper();
				unhandledExceptionHelper.Register();
				//x unhandledExceptionHelper.SetLogger(Startup.Instance.ServiceProvider.GetService<ILoggerFactory>());
				//x unhandledExceptionHelper.LogExistingCrashes(true);

				await Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
					.ConfigureServices((hostContext, services) =>
					{
#if (BackgroundService)
						services.AddGracePeriodManagerService(hostContext.Configuration);
#endif
						services.AddHelper(hostContext.Configuration);
					})
					.RunConsoleAsync();

				unhandledExceptionHelper.Unregister();

				return;
			}
#endif

			IHostBuilder hostBuilder = CreateHostBuilder(args);

#if (WindowsService)
			bool isService = !Debugger.IsAttached && args.Contains("--windowsService");
			if (isService)
			{
				hostBuilder.UseWindowsService();
			}
#endif

			IHost host = hostBuilder.Build();


			ILogger<Program> logger = host.Services.GetService<ILogger<Program>>();
			IWebHostEnvironment hostingEnvironment = host.Services.GetService<IWebHostEnvironment>();
			AssemblyName assemblyName = Assembly.GetExecutingAssembly().GetName();
			var applicationGuid = Guid.NewGuid();
			using (logger.ProgramScope(assemblyName.Name
									 , assemblyName.Version.ToString()
									 , applicationGuid.ToString()))
			{
				logger.ProgramStarted(Process.GetCurrentProcess().Id
									, Thread.CurrentThread.ManagedThreadId);

				logger.ProgramInitial(hostingEnvironment.EnvironmentName
									, EnvironmentHelper.IsDocker
									, Environment.UserInteractive
									, Debugger.IsAttached
									, false // delete this
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
#if (SerilogSupport)
			// Waiting for https://github.com/serilog/serilog-aspnetcore/issues/49
			serviceProvider.GetRequiredService<IHostApplicationLifetime>()
						   .ApplicationStopped.Register(Log.CloseAndFlush);
#endif

#if (WebApplicationExists)
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

#endif
			var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
			using (var scope = scopeFactory.CreateScope())
			{
				var helper = scope.ServiceProvider.GetService<Helper>();
				helper.Echo("Hello World!");
			}
		}

		public static IHostBuilder CreateHostBuilder(string[] args) =>
            Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
#if (SerilogSupport)
                .UseSerilog((hostingContext, loggerConfiguration) => loggerConfiguration
                        .ReadFrom.Configuration(hostingContext.Configuration)
                        .Enrich.WithExceptionDetails(new DestructuringOptionsBuilder()
                            .WithDefaultDestructurers()
                            .WithDestructurers(new IExceptionDestructurer[]
                            {
                                new SqlExceptionDestructurer(),
                                new DbUpdateExceptionDestructurer()
                            }))
				//.Enrich.FromLogContext()
				//.Enrich.WithProcessId()
				//.Enrich.WithThreadId()
				//.WriteTo.Debug()
				//.WriteTo.Console(
				//    outputTemplate:
				//    "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
				//.WriteTo.Console(new RenderedCompactJsonFormatter())
				//.WriteTo.File(new RenderedCompactJsonFormatter(), "/logs/log.ndjson")
				//.WriteTo.Seq(Environment.GetEnvironmentVariable("SEQ_URL") ?? "http://localhost:5341")
				//.MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
				)
#endif
#if (WebApiApplication)
				 .ConfigureWebHostDefaults(webBuilder =>
				 {
				     webBuilder.UseStartup<Startup>()
				         .CaptureStartupErrors(true)
				         .UseSetting("detailedErrors", "true")
				         .UseShutdownTimeout(TimeSpan.FromSeconds(5))
				         ;
				 })
#endif
				;
	}
}
