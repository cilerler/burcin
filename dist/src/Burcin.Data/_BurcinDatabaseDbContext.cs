using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Burcin.Models;

namespace Burcin.Data
{
    public partial class BurcinDatabaseDbContext : DbContext
    {
        // public BurcinDatabaseDbContext()
        // {
        // }

        ////! Burcin.Host.DbContextFactory depands on this constructor, comment out it upon using scaffold or migration
        // public BurcinDatabaseDbContext(DbContextOptions<BurcinDatabaseDbContext> options) : base(options)
        // {
        // }

        //// public virtual DbSet<T> T { get; set; }

        // protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        // {
        //     if (!optionsBuilder.IsConfigured)
        //     {
        //     }
        // }

        // protected override void OnModelCreating(ModelBuilder modelBuilder)
        // {
        //     OnModelCreatingPostActions(modelBuilder);
        // }

        partial void OnModelCreatingPostActions(ModelBuilder modelBuilder)
        {
            modelBuilder.ShadowProperties();
            SetGlobalQueryFilters(modelBuilder);
        }

        public override int SaveChanges()
        {
           ChangeTracker.SetShadowProperties();
           return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = new CancellationToken())
        {
           ChangeTracker.SetShadowProperties();
           return await base.SaveChangesAsync(cancellationToken);
        }

       private void SetGlobalQueryFilters(ModelBuilder modelBuilder)
       {
           foreach (var entityType in modelBuilder.Model.GetEntityTypes())
           {
               if (typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
               {
                   var method = _setGlobalQueryForSoftDeleteMethodInfo.MakeGenericMethod(entityType.ClrType);
                   method.Invoke(this, new object[] { modelBuilder });
               }
           }
       }

       public void SetGlobalQueryForSoftDelete<T>(ModelBuilder modelBuilder) where T : class, ISoftDelete
       {
           modelBuilder.Entity<T>().HasQueryFilter(item => !EF.Property<bool>(item, Constants.SoftDelete));
       }

       private readonly MethodInfo _setGlobalQueryForSoftDeleteMethodInfo = typeof(BurcinDatabaseDbContext).GetMethods(BindingFlags.Public | BindingFlags.Instance)
.Single(t => t.IsGenericMethod && t.Name == nameof(SetGlobalQueryForSoftDelete));
    }
}
