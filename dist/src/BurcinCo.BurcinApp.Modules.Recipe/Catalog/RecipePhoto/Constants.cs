namespace BurcinCo.BurcinApp.Modules.Recipe.Catalog.RecipePhoto;

internal static class Constants
{
	public const string ServiceName = "RecipePhoto";

	public const string OpenApiTag = "RecipePhoto";

	// Two minimal-API endpoints — the signed-URL issuer is per-recipe (entity-adjacent), the
	// download endpoint is keyed by an opaque token (not entity-bound). Different shapes, same
	// module.
	public const string SignedUrlRoute = "/api/recipes/{recipeId:long}/photo-url";
	public const string DownloadRouteGroup = "/api/photos";

	public static class Metrics
	{
		public const string MeterName = "BurcinCo.BurcinApp.Modules.Recipe.Catalog.RecipePhoto";

		public const string SignedUrlIssued = "recipe.photo.signed_url.issued";
		public const string DownloadServed = "recipe.photo.download.served";
		public const string DownloadRejected = "recipe.photo.download.rejected";
	}

	public static class Activities
	{
		public const string ActivitySourceName = "BurcinCo.BurcinApp.Modules.Recipe.Catalog.RecipePhoto";
	}

	public static class Tags
	{
		public const string RecipeId = "recipe.id";
		public const string TokenRejectedReason = "token.rejected_reason";
	}
}
