using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BurcinCo.BurcinApp.Models.BurcinDatabase
{
	[Table(nameof(CategoryGroup), Schema = Constants.DefaultSchema)]
	public partial class CategoryGroup : BaseModel
	{
		public CategoryGroup()
		{
			CategoryCodeGroupMappings = new HashSet<CategoryCodeGroupMapping>();
		}

		[Required] [StringLength(50)]
		public string Name { get; set; }

		[InverseProperty(nameof(CategoryCodeGroupMapping.Group))]
		public virtual ICollection<CategoryCodeGroupMapping> CategoryCodeGroupMappings { get; set; }
	}
}
