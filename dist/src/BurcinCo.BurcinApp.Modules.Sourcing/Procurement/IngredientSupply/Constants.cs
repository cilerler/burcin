namespace BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply;

internal static class Constants
{
	public const string ServiceName = "IngredientSupply";

	public const string RouteGroup = "/api/sourcing/quotes";

	public const string OpenApiTag = "Sourcing";

	/// <summary>
	/// Inbox consumer name for the response subscriber. Stable, hand-coded so renames don't
	/// retroactively re-process all historical messages.
	/// </summary>
	public const string ResponseConsumerName = "BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.QuoteResponse";

	public static class Metrics
	{
		public const string MeterName = "BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply";

		public const string QuoteRequested = "sourcing.ingredient_quote.requested";
		public const string QuoteSent = "sourcing.ingredient_quote.sent";
		public const string QuoteResponseReceived = "sourcing.ingredient_quote.response_received";
		public const string QuoteFailed = "sourcing.ingredient_quote.failed";
	}

	public static class Activities
	{
		public const string ActivitySourceName = "BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply";
	}

	public static class Tags
	{
		public const string QuoteId = "quote.id";
		public const string SupplierKey = "supplier.key";
	}
}
