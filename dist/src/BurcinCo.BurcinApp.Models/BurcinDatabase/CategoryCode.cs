using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BurcinCo.BurcinApp.Models.BurcinDatabase
{
	[Table("CategoryCode", Schema = "Recipe")]
	[Index(nameof(Code), Name = "IX_CategoryCode_Code", IsUnique = true)]
	public partial class CategoryCode
	{
		[Key]
		public long Id { get; set; }

		public Guid RowGuid { get; set; }

		public byte[] RowVersion { get; set; } = null!;

		public DateTime CreatedAt { get; set; }

		public DateTime ModifiedAt { get; set; }

		[StringLength(261)]
		public string ModifiedBy { get; set; } = null!;

		public short Code { get; set; }

		[StringLength(50)]
		public string Name { get; set; } = null!;

		[InverseProperty(nameof(Recipe.CategoryCodeNavigation))]
		public virtual ICollection<Recipe> Recipes { get; set; } = new List<Recipe>();

		[InverseProperty(nameof(CategoryCodeGroupMapping.CategoryCode))]
		public virtual ICollection<CategoryCodeGroupMapping> CategoryCodeGroupMappings { get; set; } = new List<CategoryCodeGroupMapping>();
	}
}
