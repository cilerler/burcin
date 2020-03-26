using Microsoft.AspNet.OData.Builder;
using Microsoft.OData.Edm;

namespace Burcin.Api
{
	public class ODataEdmModelHelper
	{
		public static IEdmModel GetEdmModel()
		{
			var modelBuilder = new ODataConventionModelBuilder();
			//odataBuilder.EntitySet<TodoItem>(nameof(TodoItem));
			return modelBuilder.GetEdmModel();
		}
	}
}
