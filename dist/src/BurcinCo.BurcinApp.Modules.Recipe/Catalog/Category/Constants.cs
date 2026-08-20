namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.Category;

internal static class Constants
{
	private const string InstrumentationName =
		$"{nameof(BurcinCo)}.{nameof(BurcinCo.BurcinApp)}.{nameof(BurcinCo.BurcinApp.Modules)}.{nameof(BurcinCo.BurcinApp.Modules.Recipe)}.{nameof(BurcinCo.BurcinApp.Modules.Recipe.Catalog)}.{nameof(BurcinCo.BurcinApp.Modules.Recipe.Catalog.Category)}";

	public const string ServiceName = nameof(BurcinCo.BurcinApp.Modules.Recipe.Catalog.Category);

	public const string RouteGroupBase = "/api/categories";
	public const string CodesRoute = "/codes";
	public const string GroupsRoute = "/groups";
	public const string MappingsRoute = "/mappings";

	public const string OpenApiTag = ServiceName;

	public static class Metrics
	{
		public const string MeterName = InstrumentationName;

		public const string CodeCreated = "recipe.category.code.created";
		public const string CodeUpdated = "recipe.category.code.updated";
		public const string CodeDeleted = "recipe.category.code.deleted";
		public const string GroupCreated = "recipe.category.group.created";
		public const string GroupUpdated = "recipe.category.group.updated";
		public const string GroupDeleted = "recipe.category.group.deleted";
		public const string MappingCreated = "recipe.category.mapping.created";
		public const string MappingDeleted = "recipe.category.mapping.deleted";
	}

	public static class Activities
	{
		public const string ActivitySourceName = InstrumentationName;
	}

	public static class Tags
	{
		public const string CategoryCodeId = "category.code.id";
		public const string CategoryGroupId = "category.group.id";
	}
}
