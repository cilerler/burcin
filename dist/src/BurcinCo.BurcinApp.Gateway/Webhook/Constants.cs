namespace BurcinCo.BurcinApp.Gateway.Webhook;

internal static class Constants
{
	public const string ServiceName = "BurcinCo.BurcinApp.Gateway.Webhook";
	public const string OpenApiTag = "Webhook";
	public const string RoutePattern = "/webhooks/{**path}";

	internal static class Metrics
	{
		public const string MeterName = ServiceName;

		public const string WebhookReceived = "app_webhook_received_total";
		public const string WebhookPublishDuration = "app_webhook_publish_duration_seconds";
		public const string WebhookPublishFailures = "app_webhook_publish_failures_total";
	}

	internal static class Activities
	{
		public const string WebhookPublish = "Webhook.Publish";
	}

	internal static class HttpClients
	{
		public const string RabbitMqManagement = "Webhook.RabbitMqManagement";
	}

	internal static class ResiliencePipelines
	{
		public const string RabbitMqManagement = "Webhook.RabbitMqManagement.Resilience";
	}

	internal static class Tags
	{
		public const string InternalServiceName = "app.service.name";
		public const string WebhookPath = "webhook.path";
		public const string Outcome = "outcome";
		public const string Reason = "reason";
	}
}
