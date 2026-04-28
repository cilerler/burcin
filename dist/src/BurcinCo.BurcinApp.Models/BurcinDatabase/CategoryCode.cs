using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BurcinCo.BurcinApp.Models.BurcinDatabase
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

		[InverseProperty(nameof(CategoryCodeGroupMapping.Code))]
		public virtual ICollection<CategoryCodeGroupMapping> CategoryCodeGroupMappings { get; set; }
	}
}
