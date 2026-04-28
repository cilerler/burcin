using System.ComponentModel.DataAnnotations;

namespace BurcinCo.BurcinApp.Gateway.Configuration;

public sealed class RabbitMqSettings
{
	public const string ConfigurationSectionName = "RabbitMq";

	/// <summary>
	/// Key into the top-level <c>ConnectionStrings</c> section. The resolved value is the RabbitMQ management
	/// HTTP endpoint with credentials embedded in the URL (e.g. <c>http://user:pass@host:15672</c>).
	/// </summary>
	[Required]
	public string ManagementConnectionStringKey { get; init; } = null!;
}
