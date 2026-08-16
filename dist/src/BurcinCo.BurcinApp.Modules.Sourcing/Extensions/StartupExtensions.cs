using System;
using BurcinCo.BurcinApp.Modules.Sourcing.Procurement.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Extensions;

/// <summary>
/// Module-level wiring for Sourcing. Demonstrates *using* reliable-messaging via <c>IOutboxPublisher</c>;
/// the Outbox/Inbox schema and EF store wiring live in Data (when Sample is on), not here, so any module
/// can use reliable-messaging without depending on Sourcing.
/// </summary>
public static class StartupExtensions
{
	public static IServiceCollection AddSourcingModule(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.AddProcurementComponent();

		return services;
	}

	public static WebApplication MapSourcingModule(
		this WebApplication app,
		bool enabled)
	{
		ArgumentNullException.ThrowIfNull(app);

		if (!enabled)
		{
			return app;
		}

		app.MapProcurementComponent(enabled);

		return app;
	}
}
