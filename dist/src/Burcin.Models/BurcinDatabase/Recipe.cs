using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Burcin.Models.BurcinDatabase
{
	[Table(nameof(Recipe), Schema = Constants.DefaultSchema)]
	public partial class Recipe : BaseModel
    {
	    public long ChefId { get; set; }
	    [ForeignKey(nameof(ChefId))]
	    [InverseProperty(nameof(BurcinDatabase.Chef.Recipes))]
	    public virtual Chef Chef { get; set; }

	    [StringLength(200)]
	    public string Name { get; set; }
		public string Url { get; set; }
		public ushort Yield { get; set; }
		public float GramPerYield { get; set; }

		[InverseProperty(nameof(RecipeExpansion.Recipe))]
	    public virtual RecipeExpansion Expansion { get; set; }

		public short? CategoryCode { get; set; }
		[ForeignKey(nameof(CategoryCode))]
	    [InverseProperty(nameof(BurcinDatabase.CategoryCode.Recipes))]
	    public virtual CategoryCode CategoryNavigation { get; set; }

	}
}
