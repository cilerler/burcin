using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BurcinCo.BurcinApp.Models.BurcinDatabase
{
	[Table(nameof(RecipeExpansion), Schema = Constants.DefaultSchema)]
	public partial class RecipeExpansion : BaseModelWithoutKey
	{
		[Key]
		public long RecipeId { get; set; }
		[ForeignKey(nameof(RecipeId))]
		[InverseProperty(nameof(BurcinDatabase.Recipe.Expansion))]
		public virtual Recipe Recipe { get; set; }

		public ushort Rate { get; set; }
		public string Notes { get; set; }
	}
}
