using BurcinCo.BurcinApp.Models.BurcinDatabase;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;

namespace BurcinCo.BurcinApp.Data
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
			modelBuilder.EntitySet<CategoryCodeGroupMapping>(nameof(CategoryCodeGroupMapping));
			return modelBuilder.GetEdmModel();
		}
	}
}
