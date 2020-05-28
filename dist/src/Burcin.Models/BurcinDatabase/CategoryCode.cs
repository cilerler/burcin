using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Burcin.Models.BurcinDatabase
{
	[Table(nameof(CategoryCode), Schema = Constants.DefaultSchema)]
	public partial class CategoryCode : BaseModel
	{
		public CategoryCode()
		{
			Recipes = new HashSet<Recipe>();
		}

		[Required]
		public short Code { get; set; }

		[Required] [StringLength(50)]
		public string Name { get; set; }

		[InverseProperty(nameof(Recipe.CategoryNavigation))]
		public virtual ICollection<Recipe> Recipes { get; set; }

		[InverseProperty(nameof(CategoryCodeGroup.Code))]
		public virtual ICollection<CategoryCodeGroup> CategoryCodeGroups { get; set; }
	}
}
