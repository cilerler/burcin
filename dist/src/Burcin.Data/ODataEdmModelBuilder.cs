using Burcin.Models.BurcinDatabase;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;

namespace Burcin.Data
{
	public class ODataEdmModelBuilder
	{
		public static IEdmModel GetEdmModel()
		{
			var modelBuilder = new ODataConventionModelBuilder();
			modelBuilder.EntitySet<Chef>(nameof(Chef));
			modelBuilder.EntitySet<Recipe>(nameof(Recipe));
			modelBuilder.EntitySet<RecipeExpansion>(nameof(RecipeExpansion));
			modelBuilder.EntitySet<CategoryCode>(nameof(CategoryCode));
			modelBuilder.EntitySet<CategoryGroup>(nameof(CategoryGroup));
			modelBuilder.EntitySet<CategoryCodeGroup>(nameof(CategoryCodeGroup));
			return modelBuilder.GetEdmModel();
		}
	}
}
