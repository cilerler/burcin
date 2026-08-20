using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BurcinCo.BurcinApp.Data;
using BurcinCo.BurcinApp.Modules.Recipe.Extensions;
using Testcontainers.MsSql;

namespace BurcinCo.BurcinApp.Modules.Recipe.Integration.Tests.Fixtures;

/// <summary>
/// Per-assembly fixture for the Recipe module tests. Spins up a single MsSql Testcontainer, applies
/// the schema from EF migrations or the current model, and exposes scoped <see cref="IServiceProvider"/>s with the Recipe
/// module DI registered. No broker — Recipe is a pure DB-backed module.
/// </summary>
internal sealed class RecipeTestFixture : IAsyncDisposable
{
	private readonly MsSqlContainer _mssql;
	private ServiceProvider? _root;
	private bool _initialized;

	public RecipeTestFixture()
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
		_root = BuildRootServices();
		await EnsureSchemaAsync().ConfigureAwait(false);
		_initialized = true;
	}

	public async ValueTask DisposeAsync()
	{
		if (_root is not null) await _root.DisposeAsync().ConfigureAwait(false);
		await _mssql.DisposeAsync().ConfigureAwait(false);
	}

	public AsyncServiceScope CreateScope()
	{
		if (_root is null) throw new InvalidOperationException("Fixture not initialized.");
		return _root.CreateAsyncScope();
	}

	/// <summary>Truncate Recipe-owned tables between tests so each test starts from a clean slate.
	/// Soft-delete triggers (INSTEAD OF DELETE) would otherwise convert these to UPDATEs and leave
	/// rows behind — wrap DELETEs with DISABLE/ENABLE TRIGGER to hard-delete during cleanup. Recipe
	/// has no soft-delete trigger (temporal table), so no enable/disable for it.</summary>
	public async Task CleanTablesAsync()
	{
		await using var scope = CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<BurcinDatabaseDbContext>();

		// Chef is the only entity with an INSTEAD OF DELETE trigger (the canonical soft-delete demo).
		// CategoryCode/CategoryGroup are hard-delete; Recipe/RecipeExpansion/CategoryCodeGroupMapping
		// have no triggers either (temporal or cascading FK — see triggers.sql header for the rules).
		await db.Database.ExecuteSqlRawAsync(
			"DISABLE TRIGGER [Recipe].[Chef_SoftDelete] ON [Recipe].[Chef];"
		).ConfigureAwait(false);
		try
		{
			// Order matters for FK constraints: dependents first.
			await db.Database.ExecuteSqlRawAsync("DELETE FROM Recipe.RecipeExpansion").ConfigureAwait(false);
			await db.Database.ExecuteSqlRawAsync("DELETE FROM Recipe.CategoryCodeGroupMapping").ConfigureAwait(false);
			await db.Database.ExecuteSqlRawAsync("DELETE FROM Recipe.Recipe").ConfigureAwait(false);
			await db.Database.ExecuteSqlRawAsync("DELETE FROM Recipe.Chef").ConfigureAwait(false);
			await db.Database.ExecuteSqlRawAsync("DELETE FROM Recipe.CategoryCode").ConfigureAwait(false);
			await db.Database.ExecuteSqlRawAsync("DELETE FROM Recipe.CategoryGroup").ConfigureAwait(false);
		}
		finally
		{
			await db.Database.ExecuteSqlRawAsync(
				"ENABLE TRIGGER [Recipe].[Chef_SoftDelete] ON [Recipe].[Chef];"
			).ConfigureAwait(false);
		}
	}

	private ServiceProvider BuildRootServices()
	{
		var services = new ServiceCollection();
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:MsSqlConnection"] = MsSqlConnectionString,
			})
			.Build();

		services.AddSingleton<IConfiguration>(config);
		services.AddLogging(b => b.AddConfiguration(config.GetSection("Logging")));
		services.AddOptions();
		services.AddMetrics(); // Registers IMeterFactory; module services use it for instrumentation.

		// Production-equivalent DbContext registration so the model matches the snapshot the migration
		// was generated from (Outbox/Inbox are part of the schema when Sample is on, owned by Data).
		// MigrationsAssemblyName is the only override test fixtures need — production applies migrations
		// via the EF CLI which pins through --project, not the runtime options.
		services.AddBurcinDatabaseDbContext(s => s.MigrationsAssemblyName =
			typeof(BurcinCo.BurcinApp.Migrations.DbContextFactory).Assembly.GetName().Name);

		services.AddRecipeModule();

		return services.BuildServiceProvider(validateScopes: true);
	}

	private async Task EnsureSchemaAsync()
	{
		await using var scope = CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<BurcinDatabaseDbContext>();
		if (db.Database.GetMigrations().Any())
		{
			await db.Database.MigrateAsync().ConfigureAwait(false);
		}
		else
		{
			await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
		}

		// Apply the soft-delete triggers after either schema-creation path.
		// triggers.sql is copied to the test exe's output directory via the csproj's <None Include=...Link=...> entry.
		var triggersSqlPath = Path.Combine(AppContext.BaseDirectory, "triggers.sql");
		var triggersSql = await File.ReadAllTextAsync(triggersSqlPath).ConfigureAwait(false);
		var result = await _mssql.ExecScriptAsync(triggersSql).ConfigureAwait(false);
		if (result.ExitCode != 0)
		{
			throw new InvalidOperationException($"triggers.sql apply failed (exit {result.ExitCode}): {result.Stderr}");
		}
	}
}
