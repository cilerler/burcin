namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.Recipe;

/// <summary>
/// Service-scoped identifiers for the Recipe service. Mirrors the dotnet-service-generator
/// convention: Metrics / Activities / Tags constants live here, scoped to this service.
/// </summary>
internal static class Constants
{
	private const string InstrumentationName =
		$"{nameof(BurcinCo)}.{nameof(BurcinCo.BurcinApp)}.{nameof(BurcinCo.BurcinApp.Modules)}.{nameof(BurcinCo.BurcinApp.Modules.Recipe)}.{nameof(BurcinCo.BurcinApp.Modules.Recipe.Catalog)}.{nameof(BurcinCo.BurcinApp.Modules.Recipe.Catalog.Recipe)}";

	public const string ServiceName = nameof(BurcinCo.BurcinApp.Modules.Recipe.Catalog.Recipe);

	public const string RouteGroup = "/api/recipes";

	public const string OpenApiTag = ServiceName;

	public static class Metrics
	{
		public const string MeterName = InstrumentationName;

		public const string Created = "recipe.recipe.created";
		public const string Updated = "recipe.recipe.updated";
		public const string Deleted = "recipe.recipe.deleted";
	}

	public static class Activities
	{
		public const string ActivitySourceName = InstrumentationName;
	}

	public static class Tags
	{
		public const string RecipeId = "recipe.id";
		public const string ChefId = "chef.id";
	}
}
