using Microsoft.AspNet.OData.Builder;
using Microsoft.OData.Edm;
using Burcin.Models.BurcinDatabase;

namespace Burcin.Api
{
	public class ODataEdmModelHelper
	{
		public static IEdmModel GetEdmModel()
		{
			var modelBuilder = new ODataConventionModelBuilder();
			modelBuilder.EntitySet<MyModel1>(nameof(MyModel1));
			modelBuilder.EntitySet<MyModel2>(nameof(MyModel2));
			modelBuilder.EntitySet<MyModel3>(nameof(MyModel3));
			return modelBuilder.GetEdmModel();
		}
	}
}
