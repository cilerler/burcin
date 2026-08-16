using System.ComponentModel.DataAnnotations;

namespace BurcinCo.BurcinApp.Gateway.Webhook.Configuration;

internal sealed class RabbitMqSettings
{
	public const string ConfigurationSectionName = "RabbitMq";

	/// <summary>
	/// Key into the top-level <c>ConnectionStrings</c> section. Keeping the credential-bearing
	/// management URI there separates it from ordinary Webhook settings and preserves the standard
	/// connection-string override path.
	/// </summary>
	[Required]
	public string ManagementConnectionStringKey { get; init; } = null!;
}
