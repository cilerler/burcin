using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Burcin.Models;
using Burcin.Models.BurcinDatabase;

namespace Burcin.Data
{
    public partial class BurcinDatabaseDbContext : DbContext
    {
        public BurcinDatabaseDbContext()
        {
        }

        public BurcinDatabaseDbContext(DbContextOptions<BurcinDatabaseDbContext> options) : base(options)
        {
        }

        public virtual DbSet<MyModel1> MyModels1 { get; set; }
        public virtual DbSet<MyModel2> MyModels2 { get; set; }
        public virtual DbSet<MyModel3> MyModels3 { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MyModel2>().ToTable("MyModel2", "dbo");

			modelBuilder.Entity<MyModel3>().HasIndex(mm=>mm.Code).IsUnique();

			modelBuilder.Entity<MyModel1MyModel2>()
				.HasKey(mm => new { mm.MyModel1Id, mm.MyModel2Id });

			modelBuilder.Entity<MyModel1MyModel2>()
				.HasOne(mm => mm.MyModel1)
				.WithMany(mm => mm.MyModel1MyModel2s)
				.HasForeignKey(mm => mm.MyModel1Id);

			modelBuilder.Entity<MyModel1MyModel2>()
				.HasOne(mm => mm.MyModel2)
				.WithMany(mm => mm.MyModel1MyModel2s)
				.HasForeignKey(mm => mm.MyModel2Id);

            OnModelCreatingPostActions(modelBuilder);
        }

        private void OnModelCreatingPostActions(ModelBuilder modelBuilder)
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
