using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BurcinCo.BurcinApp.Models.BurcinDatabase
{
	[PrimaryKey(nameof(CategoryGroupId), nameof(CategoryCodeId))]
	[Table("CategoryCodeGroupMapping", Schema = "Recipe")]
	[Index(nameof(CategoryCodeId), Name = "IX_CategoryCodeGroupMapping_CategoryCodeId")]
	public partial class CategoryCodeGroupMapping
	{
		public long CategoryCodeId { get; set; }

		public long CategoryGroupId { get; set; }

		public Guid RowGuid { get; set; }

		public byte[] RowVersion { get; set; } = null!;

		public DateTime CreatedAt { get; set; }

		public DateTime ModifiedAt { get; set; }

		[StringLength(261)]
		public string ModifiedBy { get; set; } = null!;

		// SoftDelete column intentionally absent: CategoryCodeGroupMapping has cascading FKs.
		// SQL Server forbids INSTEAD OF DELETE triggers on tables with cascading FKs.
		// As a many-to-many join table the lifecycle is parent-driven anyway — cascade from CategoryCode
		// or CategoryGroup is the right behavior, soft-delete on the join would just leak dangling rows.
		[ForeignKey(nameof(CategoryCodeId))]
		[InverseProperty(nameof(BurcinDatabase.CategoryCode.CategoryCodeGroupMappings))]
		public virtual CategoryCode CategoryCode { get; set; } = null!;

		[ForeignKey(nameof(CategoryGroupId))]
		[InverseProperty(nameof(BurcinDatabase.CategoryGroup.CategoryCodeGroupMappings))]
		public virtual CategoryGroup CategoryGroup { get; set; } = null!;
	}
}
