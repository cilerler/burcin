using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Burcin.Data;

namespace Burcin.Api
{
    public class DbContextFactory : IDesignTimeDbContextFactory<BurcinDbContext>
    {
        private const string MigrationAssemblyNameConfiguration = "Migration:AssemblyName";
        private const string DatabaseConnectionString = "DefaultConnection";

        public BurcinDbContext CreateDbContext(string[] args)
        {
            const string configurationJsonFile = "appsettings.Migration.json";
            var configuration = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
                                                          .AddJsonFile(configurationJsonFile
                                                                     , false
                                                                     , true)
                                                          .Build();
            string connectionString = configuration.GetConnectionString(DatabaseConnectionString);
            string assemblyName = configuration.GetValue(typeof(string), MigrationAssemblyNameConfiguration).ToString();

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
