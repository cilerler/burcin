#if (OnlyODataExists)
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;

namespace Burcin.Host
{
	public class ODataEdmModelBuilder
	{
		public static IEdmModel GetEdmModel()
		{
			var modelBuilder = new ODataConventionModelBuilder();

			// examples
			// modelBuilder.EntitySet<Chef>(nameof(Chef));
			// modelBuilder.EntitySet<Recipe>(nameof(Recipe));
			// modelBuilder.EntitySet<RecipeExpansion>(nameof(RecipeExpansion));

			return modelBuilder.GetEdmModel();
		}
	}
}
#endif
