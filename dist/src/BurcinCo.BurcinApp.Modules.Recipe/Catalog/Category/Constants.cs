namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.Category;

internal static class Constants
{
	public const string ServiceName = "Category";

	public const string RouteGroupBase = "/api/categories";
	public const string CodesRoute = "/codes";
	public const string GroupsRoute = "/groups";
	public const string MappingsRoute = "/mappings";

	public const string OpenApiTag = "Category";

	public static class Metrics
	{
		public const string MeterName = "BurcinCo.BurcinApp.Modules.Recipe.Catalog.Category";

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
		public const string ActivitySourceName = "BurcinCo.BurcinApp.Modules.Recipe.Catalog.Category";
	}

	public static class Tags
	{
		public const string CategoryCodeId = "category.code.id";
		public const string CategoryGroupId = "category.group.id";
	}
}
