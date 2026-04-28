using System;
using System.Threading.Tasks;

using BurcinCo.BurcinApp.Gateway.Extensions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

using Ruya.Primitives;

namespace BurcinCo.BurcinApp.Gateway;

internal static partial class Program
{
	// Scope correlator — one entry per process instance, reused across all log lines inside the scope.
	private static readonly Func<ILogger, string, IDisposable?> _beginInstanceScope =
		LoggerMessage.DefineScope<string>("{InstanceId}");

	// ReSharper disable once InconsistentNaming
	public static async Task Main(string[] args)
	{
		Startup.ConfigureCulture("en-US");
		await Startup.ValidateAndLogStartupInfoAsync();

		// Remove orchestrator-injected partial Console config that overrides
		// appsettings Console:LogLevel section (Aspire/DCP injects LOGGING__CONSOLE__FORMATTERNAME).
		Environment.SetEnvironmentVariable("LOGGING__CONSOLE__FORMATTERNAME", null);

		var builder = WebApplication.CreateBuilder(args);

		builder.AddDefaultServices();
		builder.AddCustomServices();

		var app = builder.Build();

		app.ConfigureDefaultPipeline();
		app.ConfigureCustomPipeline();

		IWebHostEnvironment appEnvironment = app.Environment;

		using (_beginInstanceScope(app.Logger, Guid.NewGuid().ToString()))
		{
			LogGatewayStartup(app.Logger, appEnvironment.EnvironmentName, Startup.EnvironmentName);
			await app.RunAsync();
		}
	}

	[LoggerMessage(
		EventId = 1000,
		Level = LogLevel.Information,
		Message = "Gateway environment: {AppEnvironment} | Startup environment: {StartupEnvironment}")]
	private static partial void LogGatewayStartup(ILogger logger, string appEnvironment, string startupEnvironment);
}
