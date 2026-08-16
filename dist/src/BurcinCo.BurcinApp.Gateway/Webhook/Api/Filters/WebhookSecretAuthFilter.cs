using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using BurcinCo.BurcinApp.Gateway.Webhook.Configuration;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace BurcinCo.BurcinApp.Gateway.Webhook.Api.Filters;

internal sealed class WebhookSecretAuthFilter : IEndpointFilter
{
	private readonly IOptionsMonitor<WebhookAuthSettings> _options;

	public WebhookSecretAuthFilter(IOptionsMonitor<WebhookAuthSettings> options)
	{
		ArgumentNullException.ThrowIfNull(options);
		_options = options;
	}

	public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(next);

		var settings = _options.CurrentValue;

		if (!settings.Required)
		{
			return await next(context).ConfigureAwait(false);
		}

		if (string.IsNullOrEmpty(settings.Secret))
		{
			return Results.Unauthorized();
		}

		var presented = context.HttpContext.Request.Headers[settings.HeaderName].ToString();
		if (string.IsNullOrEmpty(presented))
		{
			return Results.Unauthorized();
		}

		var presentedBytes = Encoding.UTF8.GetBytes(presented);
		var secretBytes = Encoding.UTF8.GetBytes(settings.Secret);

		if (presentedBytes.Length != secretBytes.Length
			|| !CryptographicOperations.FixedTimeEquals(presentedBytes, secretBytes))
		{
			return Results.Unauthorized();
		}

		return await next(context).ConfigureAwait(false);
	}
}
