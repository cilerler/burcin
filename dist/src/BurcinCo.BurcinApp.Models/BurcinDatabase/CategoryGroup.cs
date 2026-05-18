using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BurcinCo.BurcinApp.Models.BurcinDatabase
{
	[Table("CategoryGroup", Schema = "Recipe")]
	public partial class CategoryGroup
	{
		[Key]
		public long Id { get; set; }

		public Guid RowGuid { get; set; }

		public byte[] RowVersion { get; set; } = null!;

		public DateTime CreatedAt { get; set; }

		public DateTime ModifiedAt { get; set; }

		[StringLength(261)]
		public string ModifiedBy { get; set; } = null!;

		[StringLength(50)]
		public string Name { get; set; } = null!;

		[InverseProperty(nameof(CategoryCodeGroupMapping.CategoryGroup))]
		public virtual ICollection<CategoryCodeGroupMapping> CategoryCodeGroupMappings { get; set; } = new List<CategoryCodeGroupMapping>();
	}
}
