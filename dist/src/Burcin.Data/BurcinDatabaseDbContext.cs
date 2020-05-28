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

        public virtual DbSet<Chef> Chefs { get; set; }
        public virtual DbSet<Recipe> Recipes { get; set; }
		public virtual DbSet<CategoryCode> CategoryCodes { get; set; }
		public virtual DbSet<CategoryGroup> CategoryGroups { get; set; }
		public virtual DbSet<CategoryCodeGroup> CategoryCodeGroups { get; set; }
		public virtual DbSet<RecipeExpansion> RecipeExpansions { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
	        modelBuilder.Entity<Chef>().ToTable(nameof(Chef), Constants.DefaultSchema)
		        .HasIndex(e => new {e.SoftDelete, e.ModifiedAt})
		        .HasName($"IX_{nameof(Chef)}_{nameof(Chef.SoftDelete)}_{nameof(Chef.ModifiedAt)}");

	        modelBuilder.Entity<Recipe>(entity =>
	        {
		        entity.HasIndex(e => new {e.Id, e.ChefId}).IsUnique().HasName($"IX_{nameof(Recipe)}_{nameof(Recipe.Id)}_{nameof(Recipe.ChefId)}");
				entity.HasIndex(e => new { e.ModifiedAt, e.ChefId }).HasName($"IX_{nameof(Recipe)}_{nameof(Recipe.ModifiedAt)}_{nameof(Recipe.ChefId)}");

		        entity.HasOne(d => d.Chef)
			        .WithMany(p => p.Recipes)
			        .HasForeignKey(d => d.ChefId)
			        .HasConstraintName($"FK_{nameof(Recipe)}_{nameof(Chef)}");


		        entity.HasOne(d => d.CategoryNavigation)
			        .WithMany(p => p.Recipes)
			        .HasForeignKey(d => d.CategoryCode)
			        .HasPrincipalKey(p=>p.Code)
			        .OnDelete(DeleteBehavior.ClientSetNull)
			        .HasConstraintName($"FK_{nameof(Recipe)}_{nameof(CategoryCode)}");
	        });

	        modelBuilder.Entity<RecipeExpansion>(entity =>
	        {
		        entity.HasKey(e => e.RecipeId).IsClustered();
		        entity.HasOne(d => d.Recipe)
			        .WithOne(p => p.Expansion)
			        .HasForeignKey<RecipeExpansion>(d => d.RecipeId)
			        .HasConstraintName($"FK_{nameof(RecipeExpansion)}_{nameof(Recipe)}");
	        });

	        modelBuilder.Entity<CategoryCode>().HasIndex(mm => mm.Code).IsUnique();

			modelBuilder.Entity<CategoryCodeGroup>(entity =>
	        {
		        entity.HasKey(mm => new {mm.CategoryGroupId, mm.CategoryCodeId });

		        entity.HasOne(mm => mm.Group)
			        .WithMany(mm => mm.CategoryCodeGroups)
			        .HasForeignKey(mm => mm.CategoryGroupId)
			        .OnDelete(DeleteBehavior.ClientSetNull)
			        .HasConstraintName($"FK_{nameof(CategoryCodeGroup)}_{nameof(CategoryGroup)}");

		        entity
			        .HasOne(mm => mm.Code)
			        .WithMany(mm => mm.CategoryCodeGroups)
			        .HasForeignKey(mm => mm.CategoryCodeId)
			        .HasConstraintName($"FK_{nameof(CategoryCodeGroup)}_{nameof(CategoryCode)}");
	        });

            OnModelCreatingPostActions(modelBuilder);
        }

        partial void OnModelCreatingPostActions(ModelBuilder modelBuilder);
    }
}
