using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BurcinCo.BurcinApp.Models.BurcinDatabase
{
	[Table("RecipeExpansion", Schema = "Recipe")]
	public partial class RecipeExpansion
	{
		// PK is RecipeId (also the FK to Recipe). No surrogate Id — this is a 1:1 extension table.
		[Key]
		public long RecipeId { get; set; }

		public Guid RowGuid { get; set; }

		public byte[] RowVersion { get; set; } = null!;

		public DateTime CreatedAt { get; set; }

		public DateTime ModifiedAt { get; set; }

		[StringLength(261)]
		public string ModifiedBy { get; set; } = null!;

		// SoftDelete column intentionally absent: RecipeExpansion has a cascading FK to Recipe.
		// SQL Server forbids INSTEAD OF DELETE triggers on tables with cascading FKs (melis guidance).
		// RecipeExpansion's lifecycle is tied to Recipe — when Recipe deletes, the cascade hard-deletes
		// the expansion row; soft-delete on this 1:1 extension table doesn't carry useful semantics.
		public int Rate { get; set; }

		public string Notes { get; set; } = null!;

		[ForeignKey(nameof(RecipeId))]
		[InverseProperty(nameof(BurcinDatabase.Recipe.RecipeExpansion))]
		public virtual Recipe Recipe { get; set; } = null!;
	}
}
