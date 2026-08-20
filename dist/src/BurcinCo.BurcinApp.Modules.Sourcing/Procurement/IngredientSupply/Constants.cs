namespace BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply;

internal static class Constants
{
	private const string InstrumentationName =
		$"{nameof(BurcinCo)}.{nameof(BurcinCo.BurcinApp)}.{nameof(BurcinCo.BurcinApp.Modules)}.{nameof(BurcinCo.BurcinApp.Modules.Sourcing)}.{nameof(BurcinCo.BurcinApp.Modules.Sourcing.Procurement)}.{nameof(BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply)}";

	public const string ServiceName = nameof(BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply);

	public const string RouteGroup = "/api/v1/ingredient-supply";

	public const string OpenApiTag = ServiceName;

	/// <summary>
	/// Inbox consumer name for the response subscriber. Stable, hand-coded so renames don't
	/// retroactively re-process all historical messages.
	/// </summary>
	public const string ResponseConsumerName = "BurcinCo.BurcinApp.Modules.Sourcing.Procurement.IngredientSupply.QuoteResponse";

	public static class Metrics
	{
		public const string MeterName = InstrumentationName;

		public const string QuoteRequested = "app_ingredient_supply_quote_requested_total";
		public const string QuoteSent = "app_ingredient_supply_quote_sent_total";
		public const string QuoteResponseReceived = "app_ingredient_supply_quote_response_received_total";
		public const string QuoteFailed = "app_ingredient_supply_quote_failed_total";
	}

	public static class Tags
	{
		public const string InternalServiceName = "app.service.name";
		public const string QuoteId = "quote.id";
		public const string SupplierKey = "supplier.key";
		public const string HasRecipe = "quote.has_recipe";
		public const string Accepted = "quote.accepted";
		public const string FailureStage = "failure.stage";
	}

	public static class FailureStages
	{
		public const string SupplierResponse = "supplier_response";
	}

	public static class HttpClients
	{
		public const string SupplierWebhook = "IngredientSupply.SupplierWebhook";
	}

	public static class ResiliencePipelines
	{
		public const string SupplierWebhook = "IngredientSupply.SupplierWebhook";
	}
}
