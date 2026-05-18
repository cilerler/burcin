using Microsoft.EntityFrameworkCore;
using BurcinCo.BurcinApp.Models.BurcinDatabase;

namespace BurcinCo.BurcinApp.Data
{
	public partial class BurcinDatabaseDbContext : DbContext
	{
		public BurcinDatabaseDbContext(DbContextOptions<BurcinDatabaseDbContext> options) : base(options)
		{
		}

		public virtual DbSet<CategoryCode> CategoryCodes { get; set; }
		public virtual DbSet<CategoryCodeGroupMapping> CategoryCodeGroupMappings { get; set; }
		public virtual DbSet<CategoryGroup> CategoryGroups { get; set; }
		public virtual DbSet<Chef> Chefs { get; set; }
		public virtual DbSet<IngredientQuote> IngredientQuotes { get; set; }
		public virtual DbSet<NutritionFact> NutritionFacts { get; set; }
		public virtual DbSet<Recipe> Recipes { get; set; }
		public virtual DbSet<RecipeExpansion> RecipeExpansions { get; set; }

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<CategoryCode>(entity =>
			{
				entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
				entity.Property(e => e.ModifiedAt).HasDefaultValueSql("(sysutcdatetime())");
				entity.Property(e => e.ModifiedBy).HasDefaultValueSql("(suser_sname())");
				entity.Property(e => e.RowGuid).HasDefaultValueSql("(newid())");
				entity.Property(e => e.RowVersion)
					.IsRowVersion()
					.IsConcurrencyToken();
			});

			modelBuilder.Entity<CategoryCodeGroupMapping>(entity =>
			{
				entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
				entity.Property(e => e.ModifiedAt).HasDefaultValueSql("(sysutcdatetime())");
				entity.Property(e => e.ModifiedBy).HasDefaultValueSql("(suser_sname())");
				entity.Property(e => e.RowGuid).HasDefaultValueSql("(newid())");
				entity.Property(e => e.RowVersion)
					.IsRowVersion()
					.IsConcurrencyToken();

				entity.HasOne(d => d.CategoryGroup).WithMany(p => p.CategoryCodeGroupMappings).OnDelete(DeleteBehavior.ClientSetNull);
			});

			modelBuilder.Entity<CategoryGroup>(entity =>
			{
				entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
				entity.Property(e => e.ModifiedAt).HasDefaultValueSql("(sysutcdatetime())");
				entity.Property(e => e.ModifiedBy).HasDefaultValueSql("(suser_sname())");
				entity.Property(e => e.RowGuid).HasDefaultValueSql("(newid())");
				entity.Property(e => e.RowVersion)
					.IsRowVersion()
					.IsConcurrencyToken();
			});

			modelBuilder.Entity<Chef>(entity =>
			{
				entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
				entity.Property(e => e.ModifiedAt).HasDefaultValueSql("(sysutcdatetime())");
				entity.Property(e => e.ModifiedBy).HasDefaultValueSql("(suser_sname())");
				entity.Property(e => e.RowGuid).HasDefaultValueSql("(newid())");
				entity.Property(e => e.RowVersion)
					.IsRowVersion()
					.IsConcurrencyToken();
			});

			modelBuilder.Entity<IngredientQuote>(entity =>
			{
				entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
				entity.Property(e => e.ModifiedAt).HasDefaultValueSql("(sysutcdatetime())");
				entity.Property(e => e.ModifiedBy).HasDefaultValueSql("(suser_sname())");
				entity.Property(e => e.RowGuid).HasDefaultValueSql("(newid())");
				entity.Property(e => e.RowVersion)
					.IsRowVersion()
					.IsConcurrencyToken();
			});

			modelBuilder.Entity<NutritionFact>(entity =>
			{
				entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
				entity.Property(e => e.ModifiedAt).HasDefaultValueSql("(sysutcdatetime())");
				entity.Property(e => e.ModifiedBy).HasDefaultValueSql("(suser_sname())");
				entity.Property(e => e.RowGuid).HasDefaultValueSql("(newid())");
				entity.Property(e => e.RowVersion)
					.IsRowVersion()
					.IsConcurrencyToken();
			});

			modelBuilder.Entity<Recipe>(entity =>
			{
				entity.ToTable(tb => tb.IsTemporal(ttb =>
				{
					ttb.UseHistoryTable("RecipeHistory", "Recipe");
					ttb.HasPeriodStart("ValidFrom").HasColumnName("ValidFrom");
					ttb.HasPeriodEnd("ValidTo").HasColumnName("ValidTo");
				}));

				entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
				entity.Property(e => e.ModifiedAt).HasDefaultValueSql("(sysutcdatetime())");
				entity.Property(e => e.ModifiedBy).HasDefaultValueSql("(suser_sname())");
				entity.Property(e => e.RowGuid).HasDefaultValueSql("(newid())");
				entity.Property(e => e.RowVersion)
					.IsRowVersion()
					.IsConcurrencyToken();

				entity.HasOne(d => d.CategoryCodeNavigation).WithMany(p => p.Recipes)
					.HasPrincipalKey(p => p.Code)
					.HasForeignKey(d => d.CategoryCode);
			});

			modelBuilder.Entity<RecipeExpansion>(entity =>
			{
				entity.Property(e => e.RecipeId).ValueGeneratedNever();
				entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
				entity.Property(e => e.ModifiedAt).HasDefaultValueSql("(sysutcdatetime())");
				entity.Property(e => e.ModifiedBy).HasDefaultValueSql("(suser_sname())");
				entity.Property(e => e.RowGuid).HasDefaultValueSql("(newid())");
				entity.Property(e => e.RowVersion)
					.IsRowVersion()
					.IsConcurrencyToken();
			});

			OnModelCreatingPostActions(modelBuilder);
		}
	}
}
