using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BurcinCo.BurcinApp.Models.BurcinDatabase
{
	[Table("Recipe", Schema = "Recipe")]
	[Index(nameof(CategoryCode), Name = "IX_Recipe_CategoryCode")]
	[Index(nameof(ChefId), Name = "IX_Recipe_ChefId")]
	[Index(nameof(Id), nameof(ChefId), Name = "IX_Recipe_Id_ChefId", IsUnique = true)]
	[Index(nameof(ModifiedAt), nameof(ChefId), Name = "IX_Recipe_ModifiedAt_ChefId")]
	public partial class Recipe
	{
		[Key]
		public long Id { get; set; }

		public Guid RowGuid { get; set; }

		public byte[] RowVersion { get; set; } = null!;

		public DateTime CreatedAt { get; set; }

		public DateTime ModifiedAt { get; set; }

		[StringLength(261)]
		public string ModifiedBy { get; set; } = null!;

		// SoftDelete column intentionally absent: Recipe is system-versioned (temporal). Deletes are
		// captured in RecipeHistory automatically, so soft-delete would duplicate the history mechanism
		// and SQL Server forbids INSTEAD OF DELETE triggers on temporal tables anyway. ISoftDelete is
		// not implemented for this entity.
		public long ChefId { get; set; }

		[StringLength(200)]
		public string Name { get; set; } = null!;

		public string Url { get; set; } = null!;

		public int Yield { get; set; }

		public float GramPerYield { get; set; }

		public short? CategoryCode { get; set; }

		[ForeignKey(nameof(ChefId))]
		[InverseProperty(nameof(BurcinDatabase.Chef.Recipes))]
		public virtual Chef Chef { get; set; } = null!;

		[ForeignKey(nameof(CategoryCode))]
		[InverseProperty(nameof(BurcinDatabase.CategoryCode.Recipes))]
		public virtual CategoryCode? CategoryCodeNavigation { get; set; }

		[InverseProperty(nameof(BurcinDatabase.RecipeExpansion.Recipe))]
		public virtual RecipeExpansion? RecipeExpansion { get; set; }
	}
}
