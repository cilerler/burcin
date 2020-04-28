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

        //! Burcin.Api.DbContextFactory depands on this constructor, comment it upon using scaffold
        public BurcinDatabaseDbContext(DbContextOptions<BurcinDatabaseDbContext> options) : base(options)
        {
        }

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

        //! add `partial` to the line below upon using scaffold
        void OnModelCreatingPostActions(ModelBuilder modelBuilder)
        {
            modelBuilder.ShadowProperties();
            SetGlobalQueryFilters(modelBuilder);

            foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(e => typeof(ISoftDelete).IsAssignableFrom(e.ClrType)))
            {
               modelBuilder.Entity(entityType.ClrType).Property<bool>(Constants.SoftDelete).ValueGeneratedOnAdd();
            }

            foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(e => typeof(IAuditable).IsAssignableFrom(e.ClrType)))
            {
               modelBuilder.Entity(entityType.ClrType).Property<DateTime>(Constants.CreatedAt).ValueGeneratedOnAdd();
               modelBuilder.Entity(entityType.ClrType).Property<DateTime>(Constants.ModifiedAt).ValueGeneratedOnAddOrUpdate();
            }

            foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(e => typeof(IBaseEntity).IsAssignableFrom(e.ClrType)))
            {
               modelBuilder.Entity(entityType.ClrType).Property<Guid>(Constants.RowGuid).IsConcurrencyToken().HasDefaultValueSql("NEWID()").ValueGeneratedOnAddOrUpdate();
               modelBuilder.Entity(entityType.ClrType).Property<byte[]>(Constants.UpdateVersion).IsRowVersion().ValueGeneratedOnAddOrUpdate();
            }
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
                   var method = SetGlobalQueryForSoftDeleteMethodInfo.MakeGenericMethod(entityType.ClrType);
                   method.Invoke(this, new object[] { modelBuilder });
               }
           }
       }

       public void SetGlobalQueryForSoftDelete<T>(ModelBuilder modelBuilder) where T : class, ISoftDelete
       {
           modelBuilder.Entity<T>().HasQueryFilter(item => EF.Property<bool>(item, Constants.SoftDelete));
       }

       private readonly MethodInfo SetGlobalQueryForSoftDeleteMethodInfo = typeof(BurcinDatabaseDbContext).GetMethods(BindingFlags.Public | BindingFlags.Instance)
.Single(t => t.IsGenericMethod && t.Name == nameof(SetGlobalQueryForSoftDelete));
    }
}
