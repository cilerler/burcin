using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using BurcinCo.BurcinApp.Data;

namespace BurcinCo.BurcinApp.Host
{
	/// <summary>
	/// Design-time factory for EF Core tooling against the shared <see cref="BurcinDatabaseDbContext"/>.
	/// Reads <c>appsettings.Migration.json</c> for the connection string and migrations-assembly name —
	/// kept separate from the runtime <c>appsettings.json</c> so EF tooling can run without a full
	/// host build / DI graph.
	///
	/// Design-time bypasses DI, but <c>OnModelCreating</c> still runs at migration-build time — so any
	/// model contribution that lives in the DbContext's partials (e.g., Outbox/Inbox schema registration
	/// when Sample is on, in <c>_BurcinDatabaseDbContext.OnModelCreatingPostActions</c>) is automatically
	/// picked up. No explicit non-DI hook needed.
	/// </summary>
	public class DbContextFactory : IDesignTimeDbContextFactory<BurcinDatabaseDbContext>
	{
		public const string MigrationAssemblyNameConfiguration = "Migration:AssemblyName";

		public BurcinDatabaseDbContext CreateDbContext(string[] args)
		{
			const string databaseConnectionString = "MigrationConnection";
			const string configurationFileName = "appsettings.Migration.json";

			if (!File.Exists(configurationFileName))
			{
				throw new ArgumentException($"Configuration file does not exist!  Current Directory {Directory.GetCurrentDirectory()}");
			}

			IConfiguration configuration = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
			                                                         .AddJsonFile(configurationFileName
			                                                                    , false
			                                                                    , true)
			                                                         .Build();

			string connectionString = configuration.GetConnectionString(databaseConnectionString);
			string assemblyName = configuration.GetValue(typeof(string), MigrationAssemblyNameConfiguration).ToString();

			var optionsBuilder = new DbContextOptionsBuilder<BurcinDatabaseDbContext>();
			optionsBuilder.UseSqlServer(connectionString, sqlServerOptions => sqlServerOptions.MigrationsAssembly(assemblyName));

			return new BurcinDatabaseDbContext(optionsBuilder.Options);
		}
	}
}
