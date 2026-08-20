namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.Chef;

internal static class Constants
{
	private const string InstrumentationName =
		$"{nameof(BurcinCo)}.{nameof(BurcinCo.BurcinApp)}.{nameof(BurcinCo.BurcinApp.Modules)}.{nameof(BurcinCo.BurcinApp.Modules.Recipe)}.{nameof(BurcinCo.BurcinApp.Modules.Recipe.Catalog)}.{nameof(BurcinCo.BurcinApp.Modules.Recipe.Catalog.Chef)}";

	public const string ServiceName = nameof(BurcinCo.BurcinApp.Modules.Recipe.Catalog.Chef);

	public const string RouteGroup = "/api/chefs";

	public const string OpenApiTag = ServiceName;

	public static class Metrics
	{
		public const string MeterName = InstrumentationName;

		public const string Created = "recipe.chef.created";
		public const string Updated = "recipe.chef.updated";
		public const string Deleted = "recipe.chef.deleted";
	}

	public static class Activities
	{
		public const string ActivitySourceName = InstrumentationName;
	}

	public static class Tags
	{
		public const string ChefId = "chef.id";
	}
}
