using System;
using System.IO;
using BurcinCo.BurcinApp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace BurcinCo.BurcinApp.Migrations;

/// <summary>
/// Creates the shared database context for EF Core design-time commands without starting the Host.
/// The migrations project owns both this factory and its tooling-only configuration.
/// </summary>
public sealed class DbContextFactory : IDesignTimeDbContextFactory<BurcinDatabaseDbContext>
{
	private const string ConfigurationFileName = "appsettings.Migration.json";
	private const string ConnectionStringName = "MigrationConnection";
	private const string MigrationAssemblyNameConfiguration = "Migration:AssemblyName";

	public BurcinDatabaseDbContext CreateDbContext(string[] args)
	{
		var configurationPath = Path.Combine(AppContext.BaseDirectory, ConfigurationFileName);
		if (!File.Exists(configurationPath))
		{
			throw new InvalidOperationException($"Migration configuration does not exist at '{configurationPath}'.");
		}

		var configuration = new ConfigurationBuilder()
			.SetBasePath(AppContext.BaseDirectory)
			.AddJsonFile(ConfigurationFileName, optional: false, reloadOnChange: false)
			.AddUserSecrets<DbContextFactory>(optional: true)
			.AddEnvironmentVariables()
			.Build();

		var connectionString = configuration.GetConnectionString(ConnectionStringName)
			?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is required.");
		var assemblyName = configuration[MigrationAssemblyNameConfiguration]
			?? throw new InvalidOperationException($"Configuration value '{MigrationAssemblyNameConfiguration}' is required.");

		var optionsBuilder = new DbContextOptionsBuilder<BurcinDatabaseDbContext>();
		optionsBuilder.UseSqlServer(
			connectionString,
			sqlServerOptions => sqlServerOptions.MigrationsAssembly(assemblyName));

		return new BurcinDatabaseDbContext(optionsBuilder.Options);
	}
}
