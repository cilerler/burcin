using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Burcin.Models.BurcinDatabase
{
	[Table(nameof(CategoryGroup), Schema = Constants.DefaultSchema)]
	public partial class CategoryGroup : BaseModel
	{
		public CategoryGroup()
		{
			CategoryCodeGroups = new HashSet<CategoryCodeGroup>();
		}

		[Required] [StringLength(50)]
		public string Name { get; set; }

		[InverseProperty(nameof(CategoryCodeGroup.Group))]
		public virtual ICollection<CategoryCodeGroup> CategoryCodeGroups { get; set; }
	}
}
