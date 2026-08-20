using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using BurcinCo.BurcinApp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BurcinCo.BurcinApp.AppHost.E2E.Tests.Fixtures;

/// <summary>
/// Initializes the Aspire-owned application database before an HTTP test uses database-backed routes.
/// A pristine scaffold has no migration classes, so it creates the model directly until the first migration exists.
/// </summary>
internal static class DatabaseSchemaInitializer
{
	public static async Task InitializeAsync(
		DistributedApplication app,
		TimeSpan timeout,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(app);
		using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeoutSource.CancelAfter(timeout);

		var connectionString = await app
			.GetConnectionStringAsync("BurcinDatabase", timeoutSource.Token)
			.ConfigureAwait(false);
		if (string.IsNullOrWhiteSpace(connectionString))
		{
			throw new InvalidOperationException("Aspire did not supply the BurcinDatabase connection string.");
		}

		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:MsSqlConnection"] = connectionString,
			})
			.Build();
		var services = new ServiceCollection();
		services.AddSingleton<IConfiguration>(configuration);
		services.AddLogging();
		services.AddBurcinDatabaseDbContext(
			settings => settings.MigrationsAssemblyName =
				typeof(BurcinCo.BurcinApp.Migrations.DbContextFactory).Assembly.GetName().Name);

		await using var provider = services.BuildServiceProvider(validateScopes: true);
		await using var scope = provider.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<BurcinDatabaseDbContext>();
		if (db.Database.GetMigrations().Any())
		{
			await db.Database.MigrateAsync(timeoutSource.Token).ConfigureAwait(false);
		}
		else
		{
			await db.Database.EnsureCreatedAsync(timeoutSource.Token).ConfigureAwait(false);
		}

		var triggerScript = await File.ReadAllTextAsync(
			Path.Combine(AppContext.BaseDirectory, "triggers.sql"),
			timeoutSource.Token).ConfigureAwait(false);
		foreach (var batch in Regex.Split(
			triggerScript,
			@"^\s*GO\s*$",
			RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant))
		{
			if (!string.IsNullOrWhiteSpace(batch))
			{
				await db.Database.OpenConnectionAsync(timeoutSource.Token).ConfigureAwait(false);
				try
				{
					await using var command = db.Database.GetDbConnection().CreateCommand();
					command.CommandText = batch;
					await command.ExecuteNonQueryAsync(timeoutSource.Token).ConfigureAwait(false);
				}
				finally
				{
					await db.Database.CloseConnectionAsync().ConfigureAwait(false);
				}
			}
		}
	}
}
