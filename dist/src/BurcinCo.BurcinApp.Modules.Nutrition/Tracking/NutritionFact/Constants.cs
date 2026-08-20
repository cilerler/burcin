namespace BurcinCo.BurcinApp.Modules.Nutrition.Tracking.NutritionFact;

internal static class Constants
{
	private const string InstrumentationName =
		$"{nameof(BurcinCo)}.{nameof(BurcinCo.BurcinApp)}.{nameof(BurcinCo.BurcinApp.Modules)}.{nameof(BurcinCo.BurcinApp.Modules.Nutrition)}.{nameof(BurcinCo.BurcinApp.Modules.Nutrition.Tracking)}.{nameof(BurcinCo.BurcinApp.Modules.Nutrition.Tracking.NutritionFact)}";

	public const string ServiceName = nameof(BurcinCo.BurcinApp.Modules.Nutrition.Tracking.NutritionFact);

	public const string RouteGroup = "/api/nutrition";

	public const string OpenApiTag = ServiceName;

	public static class Metrics
	{
		public const string MeterName = InstrumentationName;

		public const string Created = "nutrition.fact.created";
		public const string Updated = "nutrition.fact.updated";
		public const string Deleted = "nutrition.fact.deleted";
	}

	public static class Activities
	{
		public const string ActivitySourceName = InstrumentationName;
	}

	public static class Tags
	{
		public const string RecipeId = "recipe.id";
		public const string NutritionFactId = "nutrition.fact.id";
	}
}
