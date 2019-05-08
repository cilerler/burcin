using Microsoft.EntityFrameworkCore;
using Burcin.Models;

namespace Burcin.Data
{
    public partial class BurcinDatabaseDbContext : DbContext
    {
        // public BurcinDatabaseDbContext()
        // {
        // }

        public BurcinDatabaseDbContext(DbContextOptions<BurcinDatabaseDbContext> options) : base(options)
        {
        }

        // public virtual DbSet<T> T { get; set; }

        // protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        // {
        //     if (!optionsBuilder.IsConfigured)
        //     {
        //     }
        // }

        // protected override void OnModelCreating(ModelBuilder modelBuilder)
        // {
        // }
    }
}
