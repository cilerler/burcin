using System;
using System.Threading.Tasks;
using BurcinCo.BurcinApp.Gateway.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
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
		// appsettings Console:LogLevel section (Aspire/DCP injects LOGGING__CONSOLE__FORMATTERNAME)
		Environment.SetEnvironmentVariable("LOGGING__CONSOLE__FORMATTERNAME", null);

		var builder = WebApplication.CreateBuilder(args);

		builder.AddDefaultServices();

		// Capture graph-changing configuration once. Registration and endpoint mapping use the
		// same immutable instance, so a reload cannot produce a half-changed Gateway graph.
		var capabilities = builder.Configuration
			.GetRequiredSection(CapabilitySelection.ConfigurationSectionName)
			.Get<CapabilitySelection>()
			?? throw new InvalidOperationException(
				$"{CapabilitySelection.ConfigurationSectionName} configuration is required.");

		builder.AddCustomServices(capabilities);

		var app = builder.Build();

		app.ConfigureDefaultPipeline();
		app.ConfigureCustomPipeline();

		IWebHostEnvironment appEnvironment = app.Environment;

		using (_beginInstanceScope(app.Logger, Guid.NewGuid().ToString()))
		{
			LogApplicationStartup(app.Logger, appEnvironment.EnvironmentName, Startup.EnvironmentName);
			await app.RunAsync();
		}
	}

	[LoggerMessage(
		EventId = 1000,
		Level = LogLevel.Information,
		Message = "Application environment: {AppEnvironment} | Startup environment: {StartupEnvironment}")]
	private static partial void LogApplicationStartup(ILogger logger, string appEnvironment, string startupEnvironment);
}
