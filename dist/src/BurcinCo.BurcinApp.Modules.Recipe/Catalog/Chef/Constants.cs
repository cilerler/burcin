namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.Chef;

internal static class Constants
{
	public const string ServiceName = "Chef";

	public const string RouteGroup = "/api/chefs";

	public const string OpenApiTag = "Chef";

	public static class Metrics
	{
		public const string MeterName = "BurcinCo.BurcinApp.Modules.Recipe.Catalog.Chef";

		public const string Created = "recipe.chef.created";
		public const string Updated = "recipe.chef.updated";
		public const string Deleted = "recipe.chef.deleted";
	}

	public static class Activities
	{
		public const string ActivitySourceName = "BurcinCo.BurcinApp.Modules.Recipe.Catalog.Chef";
	}

	public static class Tags
	{
		public const string ChefId = "chef.id";
	}
}
