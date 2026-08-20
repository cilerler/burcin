namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.Tag;

internal static class Constants
{
	private const string InstrumentationName =
		$"{nameof(BurcinCo)}.{nameof(BurcinCo.BurcinApp)}.{nameof(BurcinCo.BurcinApp.Modules)}.{nameof(BurcinCo.BurcinApp.Modules.Recipe)}.{nameof(BurcinCo.BurcinApp.Modules.Recipe.Catalog)}.{nameof(BurcinCo.BurcinApp.Modules.Recipe.Catalog.Tag)}";

	public const string ServiceName = nameof(BurcinCo.BurcinApp.Modules.Recipe.Catalog.Tag);

	public const string OpenApiTag = ServiceName;

	public static class Metrics
	{
		public const string MeterName = InstrumentationName;

		public const string Created = "recipe.tag.created";
		public const string Updated = "recipe.tag.updated";
		public const string Deleted = "recipe.tag.deleted";
	}

	public static class Activities
	{
		public const string ActivitySourceName = InstrumentationName;
	}

	public static class Tags
	{
		public const string TagId = "tag.id";
	}
}
