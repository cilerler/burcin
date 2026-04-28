namespace BurcinCo.BurcinApp.Gateway;

internal static class Constants
{
	public const string ServiceName = "BurcinCo.BurcinApp.Gateway";

	internal static class Metrics
	{
		public const string MeterName = ServiceName;

		public const string WebhookReceived = "gateway.webhook.received";
		public const string WebhookPublishDuration = "gateway.webhook.publish.duration";
		public const string WebhookPublishFailures = "gateway.webhook.publish.failures";
	}

	internal static class Activities
	{
		public const string WebhookPublish = "Gateway.Webhook.Publish";
	}

	internal static class HttpClients
	{
		public const string RabbitMqManagement = "rabbitmq-management";
	}

	internal static class Tags
	{
		public const string WebhookPath = "webhook.path";
		public const string Outcome = "outcome";
		public const string Reason = "reason";
	}

	internal static class ConfigurationSections
	{
		public const string ReverseProxy = nameof(ReverseProxy);
	}
}
