namespace BurcinCo.BurcinApp.Modules.Nutrition.Tracking.NutritionFact;

internal static class Constants
{
	public const string ServiceName = "NutritionFact";

	public const string RouteGroup = "/api/nutrition";

	public const string OpenApiTag = "NutritionFact";

	public static class Metrics
	{
		public const string MeterName = "BurcinCo.BurcinApp.Modules.Nutrition.Tracking.NutritionFact";

		public const string Created = "nutrition.fact.created";
		public const string Updated = "nutrition.fact.updated";
		public const string Deleted = "nutrition.fact.deleted";
	}

	public static class Activities
	{
		public const string ActivitySourceName = "BurcinCo.BurcinApp.Modules.Nutrition.Tracking.NutritionFact";
	}

	public static class Tags
	{
		public const string RecipeId = "recipe.id";
		public const string NutritionFactId = "nutrition.fact.id";
	}
}
