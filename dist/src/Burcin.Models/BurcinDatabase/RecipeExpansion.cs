using System.ComponentModel.DataAnnotations.Schema;

namespace Burcin.Models.BurcinDatabase
{
	[Table(nameof(RecipeExpansion), Schema = Constants.DefaultSchema)]
	public partial class RecipeExpansion : BaseModel
	{
		public long RecipeId { get; set; }
		[ForeignKey(nameof(RecipeId))]
		[InverseProperty(nameof(Burcin.Recipe.Expansion))]
		public virtual Recipe Recipe { get; set; }

		public ushort Rate { get; set; }
		public string Notes { get; set; }
	}
}
