using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using BurcinCo.BurcinApp.Data;

namespace BurcinCo.BurcinApp.Host
{
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
