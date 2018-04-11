﻿using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ruya.ConsoleHost;
using Burcin.Data;

namespace Burcin.Api
{
    public class DbContextFactory : IDesignTimeDbContextFactory<BurcinDbContext>
    {
        private const string MigrationAssemblyNameConfiguration = "Migration:AssemblyName";
        private const string DatabaseConnectionString = "DefaultConnection";

        public BurcinDbContext CreateDbContext(string[] args)
        {
            Ruya.Primitives.EnvironmentHelper.EnvironmentName = "Migration"; //! Do not change the environment to `Development`
            string connectionString = Startup.Instance.Configuration.GetConnectionString(DatabaseConnectionString);
            string assemblyName = Startup.Instance.Configuration.GetValue(typeof(string), MigrationAssemblyNameConfiguration).ToString();

            var optionsBuilder = new DbContextOptionsBuilder<BurcinDbContext>();
            optionsBuilder.UseSqlServer(connectionString, sqlServerOptions => sqlServerOptions.MigrationsAssembly(assemblyName));
            return new BurcinDbContext(optionsBuilder.Options);
        }

        public static void RegisterExternalServices(IServiceCollection serviceCollection, IConfiguration configuration)
        {
            string connectionString = configuration.GetConnectionString(DatabaseConnectionString);
            string assemblyName = configuration.GetValue(typeof(string), MigrationAssemblyNameConfiguration).ToString();

            serviceCollection.AddDbContext<BurcinDbContext>(options => options.UseSqlServer(connectionString,
                    sqlServerOptions => {
                                            sqlServerOptions.MigrationsAssembly(assemblyName);
                                            sqlServerOptions.EnableRetryOnFailure(maxRetryCount: 5,
                                                                            maxRetryDelay: TimeSpan.FromSeconds(30),
                                                                            errorNumbersToAdd: null);
                                        }));
        }
    }
}
