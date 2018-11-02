using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Burcin.Data;

namespace Burcin.Api
{
	public class DbContextFactory : IDesignTimeDbContextFactory<BurcinDbContext>
	{
		public const string MigrationAssemblyNameConfiguration = "Migration:AssemblyName";

		public BurcinDbContext CreateDbContext(string[] args)
		{
			const string databaseConnectionString = "MigrationConnection";
			const string configurationFileName = "appsettings.json";

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

            var optionsBuilder = new DbContextOptionsBuilder<BurcinDbContext>();
            optionsBuilder.UseSqlServer(connectionString, sqlServerOptions => sqlServerOptions.MigrationsAssembly(assemblyName));
            return new BurcinDbContext(optionsBuilder.Options);
		}
	}
}
