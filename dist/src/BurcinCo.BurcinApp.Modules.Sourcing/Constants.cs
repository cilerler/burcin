namespace BurcinCo.BurcinApp.Modules.Sourcing;

/// <summary>
/// Module-wide constants for the Sourcing module — the reference implementation of the
/// outbound producer (Outbox → broker → external HTTP) and inbound consumer
/// (external HTTP → Gateway → broker → Inbox dedup → handler) flows.
/// </summary>
internal static class Constants
{
	public const string ModuleName = "Sourcing";

	public static readonly string FeatureFlag = $"Modules.{ModuleName}";

	public const string ConfigurationSectionName = "Modules:Sourcing";

	/// <summary>
	/// Topic strings used at the broker. Outbound topics are produced by Outbox + dispatcher;
	/// inbound topics match the routing key the Gateway publishes on receipt of an external webhook.
	/// </summary>
	public static class Topics
	{
		/// <summary>Internal: produced by the Outbox dispatcher when a quote is requested.</summary>
		public const string IngredientQuoteRequested = "sourcing.ingredient-quote.requested";

		/// <summary>
		/// Inbound: matches the Gateway's <c>webhooks.{path-with-dots}</c> routing key for
		/// POST /webhooks/sourcing/quote-response. The supplier's webhook URL must hit that path.
		/// </summary>
		public const string IngredientQuoteResponseReceivedFromGateway = "webhooks.sourcing.quote-response";
	}
}
