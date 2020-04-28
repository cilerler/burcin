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

        partial void OnModelCreatingPostActions(ModelBuilder modelBuilder);
    }
}
