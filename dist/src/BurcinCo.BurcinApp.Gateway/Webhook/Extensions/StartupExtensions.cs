using System;
using System.Net.Http.Headers;
using System.Text;

using BurcinCo.BurcinApp.Gateway.Webhook.Api.Filters;
using BurcinCo.BurcinApp.Gateway.Webhook.Configuration;
using BurcinCo.BurcinApp.Gateway.Webhook.Contracts;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BurcinCo.BurcinApp.Gateway.Webhook.Extensions;

/// <summary>
/// Service-level registration for the Webhook component of the Gateway. Per the
/// dotnet-service-generator skill, each service owns its full registration graph (settings,
/// contracts implementation, supporting filters, typed clients) and the composition root
/// (Gateway's <c>ProgramExtensionsCustom</c>) only needs to call <c>AddWebhookService(config)</c>.
/// </summary>
internal static class StartupExtensions
{
	public static IServiceCollection AddWebhookService(this IServiceCollection services, IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		services.AddOptions<WebhookSettings>()
			.BindConfiguration(WebhookSettings.ConfigurationSectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.AddOptions<WebhookAuthSettings>()
			.BindConfiguration(WebhookAuthSettings.ConfigurationSectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.AddOptions<RabbitMqSettings>()
			.BindConfiguration(RabbitMqSettings.ConfigurationSectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.AddSingleton<IWebhookService, WebhookService>();
		services.AddSingleton<WebhookSecretAuthFilter>();

		// Typed HttpClient for the RabbitMQ management API. Today's webhook sink is RabbitMQ — when
		// MSSQL or another transport gets added, this typed client either gains a sibling or moves
		// behind a sink-strategy abstraction.
		services.AddHttpClient(Constants.HttpClients.RabbitMqManagement, (sp, client) =>
		{
			var settings = sp.GetRequiredService<IOptions<RabbitMqSettings>>().Value;
			var managementUrl = configuration.GetConnectionString(settings.ManagementConnectionStringKey)
				?? throw new InvalidOperationException(
					$"ConnectionStrings:{settings.ManagementConnectionStringKey} is not configured.");

			var uri = new Uri(managementUrl);
			// BaseAddress must not include userinfo; strip before assigning, capture for Basic auth.
			client.BaseAddress = new UriBuilder(uri) { UserName = string.Empty, Password = string.Empty }.Uri;
			if (!string.IsNullOrEmpty(uri.UserInfo))
			{
				client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
					"Basic",
					Convert.ToBase64String(Encoding.UTF8.GetBytes(Uri.UnescapeDataString(uri.UserInfo))));
			}
		});

		return services;
	}
}
