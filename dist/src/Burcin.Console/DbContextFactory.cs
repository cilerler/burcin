using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Ruya.Extensions.DependencyInjection;
using Burcin.Data;

namespace Burcin.Console
{
    public class DbContextFactory : IDesignTimeDbContextFactory<BurcinDbContext>
    {
        public const string MigrationAssemblyNameConfiguration = "Migration:AssemblyName";
        public const string DatabaseConnectionString = "DefaultConnection";

        public BurcinDbContext CreateDbContext(string[] args)
        {
            string connectionString = Startup.Instance.Configuration.GetConnectionString(DatabaseConnectionString);
            string assemblyName = Startup.Instance.Configuration.GetValue(typeof(string), MigrationAssemblyNameConfiguration).ToString();

            var optionsBuilder = new DbContextOptionsBuilder<BurcinDbContext>();
            optionsBuilder.UseSqlServer(connectionString, sqlServerOptions => sqlServerOptions.MigrationsAssembly(assemblyName));
            return new BurcinDbContext(optionsBuilder.Options);
        }
    }
}
