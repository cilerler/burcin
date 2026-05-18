using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BurcinCo.BurcinApp.Data;
using BurcinCo.BurcinApp.Modules.Nutrition.Extensions;
using BurcinCo.BurcinApp.Modules.Recipe.Abstractions.Interfaces;
using BurcinCo.BurcinApp.Modules.Recipe.Extensions;
using Testcontainers.MsSql;

namespace BurcinCo.BurcinApp.Modules.Nutrition.Integration.Tests.Fixtures;

/// <summary>
/// Per-assembly fixture for the Nutrition module. Spins up MsSql once and exposes two scope-creation
/// modes:
///   <list type="bullet">
///     <item><see cref="CreateScopeWithLocalRecipe"/> — Modules.Recipe runs in-process (typical
///       single-image dev/prod); IRecipeService binds to the local <c>RecipeService</c>.</item>
///     <item><see cref="BuildRemoteRecipeRoot"/> — Modules.Recipe is remote; IRecipeService binds
///       to <c>RecipeClient</c> with the supplied stubbed HTTP handler. Used by the cross-module
///       HTTP test that validates the RecipeClient over the wire.</item>
///   </list>
/// </summary>
internal sealed class NutritionTestFixture : IAsyncDisposable
{
	private readonly MsSqlContainer _mssql;
	private ServiceProvider? _localRecipeRoot;
	private bool _initialized;

	public NutritionTestFixture()
	{
		_mssql = new MsSqlBuilder("cilerler/mssql-server-linux:2025-RTM-ubuntu-22.04")
			.WithPassword("YourStrong!Passw0rd")
			.Build();
	}

	public string MsSqlConnectionString => _mssql.GetConnectionString();

	public async Task InitializeAsync()
	{
		if (_initialized) return;
		await _mssql.StartAsync().ConfigureAwait(false);
		_localRecipeRoot = BuildLocalRecipeServices();
		await EnsureSchemaAsync().ConfigureAwait(false);
		_initialized = true;
	}

	public async ValueTask DisposeAsync()
	{
		if (_localRecipeRoot is not null) await _localRecipeRoot.DisposeAsync().ConfigureAwait(false);
		await _mssql.DisposeAsync().ConfigureAwait(false);
	}

	public AsyncServiceScope CreateScopeWithLocalRecipe()
	{
		if (_localRecipeRoot is null) throw new InvalidOperationException("Fixture not initialized.");
		return _localRecipeRoot.CreateAsyncScope();
	}

	/// <summary>
	/// Build a fresh per-test root with Modules.Recipe disabled, so Nutrition's StartupExtensions
	/// resolves IRecipeService to the HTTP <c>RecipeClient</c>. The supplied <paramref name="handler"/>
	/// is wired as the primary HttpMessageHandler so the test fully controls what "Recipe over HTTP"
	/// returns.
	/// </summary>
	public ServiceProvider BuildRemoteRecipeRoot(HttpMessageHandler handler)
	{
		ArgumentNullException.ThrowIfNull(handler);
		var services = new ServiceCollection();
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:MsSqlConnection"] = MsSqlConnectionString,
				// Recipe flag OFF → Nutrition binds IRecipeService to RecipeClient (HTTP).
				["FeatureManagement:Modules.Recipe"] = "false",
				// RecipeClientSettings binds; supply a non-empty BaseAddress so DataAnnotation [Required] passes.
				["Modules:Nutrition:Tracking:NutritionFact:Clients:Recipe:BaseAddress"] = "http://recipe-deployment.test/",
				["Modules:Nutrition:Tracking:NutritionFact:Clients:Recipe:TimeoutSeconds"] = "10",
			})
			.Build();

		services.AddSingleton<IConfiguration>(config);
		services.AddLogging(b => b.AddConfiguration(config.GetSection("Logging")));
		services.AddOptions();
		services.AddMetrics();

		services.AddBurcinDatabaseDbContext(s => s.MigrationsAssemblyName = "BurcinCo.BurcinApp.Migrations");

		services.AddNutritionModule(config);

		// Override the IRecipeService HttpClient's primary handler with the test stub.
		services.AddHttpClient<IRecipeService, BurcinCo.BurcinApp.Modules.Nutrition.Tracking.NutritionFact.Clients.RecipeClient>()
			.ConfigurePrimaryHttpMessageHandler(_ => handler);

		return services.BuildServiceProvider(validateScopes: true);
	}

	public async Task CleanTablesAsync()
	{
		await using var scope = CreateScopeWithLocalRecipe();
		var db = scope.ServiceProvider.GetRequiredService<BurcinDatabaseDbContext>();

		// Chef is the only entity with an INSTEAD OF DELETE trigger (the canonical soft-delete demo).
		// NutritionFact is hard-delete; Recipe and RecipeExpansion have no triggers either
		// (temporal or cascading FK — see triggers.sql header for the rules).
		await db.Database.ExecuteSqlRawAsync(
			"DISABLE TRIGGER [Recipe].[Chef_SoftDelete] ON [Recipe].[Chef];"
		).ConfigureAwait(false);
		try
		{
			// Order: Nutrition row → Recipe row → Chef row (FK chain)
			await db.Database.ExecuteSqlRawAsync("DELETE FROM Nutrition.NutritionFact").ConfigureAwait(false);
			await db.Database.ExecuteSqlRawAsync("DELETE FROM Recipe.RecipeExpansion").ConfigureAwait(false);
			await db.Database.ExecuteSqlRawAsync("DELETE FROM Recipe.Recipe").ConfigureAwait(false);
			await db.Database.ExecuteSqlRawAsync("DELETE FROM Recipe.Chef").ConfigureAwait(false);
		}
		finally
		{
			await db.Database.ExecuteSqlRawAsync(
				"ENABLE TRIGGER [Recipe].[Chef_SoftDelete] ON [Recipe].[Chef];"
			).ConfigureAwait(false);
		}
	}

	private ServiceProvider BuildLocalRecipeServices()
	{
		var services = new ServiceCollection();
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:MsSqlConnection"] = MsSqlConnectionString,
				// Recipe flag ON → IRecipeService binds to in-process RecipeService.
				["FeatureManagement:Modules.Recipe"] = "true",
			})
			.Build();

		services.AddSingleton<IConfiguration>(config);
		services.AddLogging(b => b.AddConfiguration(config.GetSection("Logging")));
		services.AddOptions();
		services.AddMetrics();

		services.AddBurcinDatabaseDbContext(s => s.MigrationsAssemblyName = "BurcinCo.BurcinApp.Migrations");

		// Recipe must be registered first because Nutrition's wiring inspects the Modules.Recipe flag
		// and either expects the in-process IRecipeService binding to be present or registers RecipeClient.
		services.AddRecipeModule(config);
		services.AddNutritionModule(config);

		return services.BuildServiceProvider(validateScopes: true);
	}

	private async Task EnsureSchemaAsync()
	{
		await using var scope = CreateScopeWithLocalRecipe();
		var db = scope.ServiceProvider.GetRequiredService<BurcinDatabaseDbContext>();
		await db.Database.MigrateAsync().ConfigureAwait(false);

		// Apply post-migration soft-delete triggers so tests run against production-equivalent DB behavior.
		var triggersSqlPath = Path.Combine(AppContext.BaseDirectory, "triggers.sql");
		var triggersSql = await File.ReadAllTextAsync(triggersSqlPath).ConfigureAwait(false);
		var result = await _mssql.ExecScriptAsync(triggersSql).ConfigureAwait(false);
		if (result.ExitCode != 0)
		{
			throw new InvalidOperationException($"triggers.sql apply failed (exit {result.ExitCode}): {result.Stderr}");
		}
	}
}

/// <summary>Test stub that records HTTP requests and returns a configurable response.</summary>
internal sealed class StubRecipeBackend : HttpMessageHandler
{
	private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
	private readonly List<HttpRequestMessage> _requests = new();

	public StubRecipeBackend(Func<HttpRequestMessage, HttpResponseMessage> responder)
	{
		_responder = responder ?? throw new ArgumentNullException(nameof(responder));
	}

	public IReadOnlyList<HttpRequestMessage> ReceivedRequests => _requests;

	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		_requests.Add(request);
		return Task.FromResult(_responder(request));
	}
}
