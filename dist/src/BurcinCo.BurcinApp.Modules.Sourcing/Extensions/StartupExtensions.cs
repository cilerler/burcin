using System;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Extensions;

/// <summary>
/// Module-level wiring for Sourcing. Demonstrates *using* reliable-messaging via <c>IOutboxPublisher</c>;
/// the Outbox/Inbox schema and EF store wiring live in Data (when Sample is on), not here, so any module
/// can use reliable-messaging without depending on Sourcing.
/// </summary>
public static class StartupExtensions
{
	public static IServiceCollection AddSourcingModule(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		services.AddProcurementComponent(configuration);

		return services;
	}

	public static IEndpointRouteBuilder MapSourcingModule(this IEndpointRouteBuilder endpoints)
	{
		ArgumentNullException.ThrowIfNull(endpoints);

		endpoints.MapProcurementComponent();

		return endpoints;
	}
}
