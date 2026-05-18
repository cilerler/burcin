namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.Tag;

internal static class Constants
{
	public const string ServiceName = "Tag";

	public const string OpenApiTag = "Tag";

	public static class Metrics
	{
		public const string MeterName = "BurcinCo.BurcinApp.Modules.Recipe.Catalog.Tag";

		public const string Created = "recipe.tag.created";
		public const string Updated = "recipe.tag.updated";
		public const string Deleted = "recipe.tag.deleted";
	}

	public static class Activities
	{
		public const string ActivitySourceName = "BurcinCo.BurcinApp.Modules.Recipe.Catalog.Tag";
	}

	public static class Tags
	{
		public const string TagId = "tag.id";
	}
}
