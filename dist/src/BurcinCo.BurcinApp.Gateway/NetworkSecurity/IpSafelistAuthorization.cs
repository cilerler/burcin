using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;

namespace BurcinCo.BurcinApp.Gateway.NetworkSecurity;

internal sealed class IpSafelistRequirement(
	bool enabled,
	IPNetwork[] allowedNetworks) : IAuthorizationRequirement
{
	public bool Enabled { get; } = enabled;

	public IPNetwork[] AllowedNetworks { get; } = allowedNetworks;
}

internal sealed class IpSafelistAuthorizationHandler : AuthorizationHandler<IpSafelistRequirement>
{
	protected override Task HandleRequirementAsync(
		AuthorizationHandlerContext context,
		IpSafelistRequirement requirement)
	{
		if (!requirement.Enabled)
		{
			context.Succeed(requirement);
			return Task.CompletedTask;
		}

		if (context.Resource is not HttpContext httpContext)
		{
			return Task.CompletedTask;
		}

		var address = httpContext.Connection.RemoteIpAddress;
		if (address is null)
		{
			return Task.CompletedTask;
		}

		if (address.IsIPv4MappedToIPv6)
		{
			address = address.MapToIPv4();
		}

		if (requirement.AllowedNetworks.Any(network =>
			network.BaseAddress.AddressFamily == address.AddressFamily && network.Contains(address)))
		{
			context.Succeed(requirement);
		}

		return Task.CompletedTask;
	}
}

internal sealed class IpSafelistAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
	private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

	public Task HandleAsync(
		RequestDelegate next,
		HttpContext context,
		AuthorizationPolicy policy,
		PolicyAuthorizationResult authorizeResult)
	{
		if (!authorizeResult.Succeeded && policy.Requirements.OfType<IpSafelistRequirement>().Any())
		{
			context.Response.StatusCode = StatusCodes.Status403Forbidden;
			return Task.CompletedTask;
		}

		return _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
	}
}
