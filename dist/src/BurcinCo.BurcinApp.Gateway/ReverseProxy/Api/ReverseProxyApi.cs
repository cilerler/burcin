using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace BurcinCo.BurcinApp.Gateway.ReverseProxy.Api;

/// <summary>
/// Pipeline entry point for Gateway's ReverseProxy feature. Wraps YARP's <c>MapReverseProxy()</c>
/// so the composition root's <c>ProgramExtensionsCustom.ConfigureCustomPipeline</c> calls
/// <c>MapReverseProxyApi()</c> — matches the <c>Api/{ServiceName}Api.cs</c> shape used by the
/// Webhook service. Named with the <c>Api</c> suffix specifically to avoid collision with YARP's
/// own <c>MapReverseProxy()</c> extension on <see cref="IEndpointRouteBuilder"/>.
/// </summary>
internal static class ReverseProxyApi
{
	public static IEndpointRouteBuilder MapReverseProxyApi(this IEndpointRouteBuilder endpoints)
	{
		endpoints.MapReverseProxy();
		return endpoints;
	}
}
