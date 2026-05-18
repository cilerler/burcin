using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BurcinCo.BurcinApp.Data;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Integration.Tests.Deployment;

/// <summary>
/// Regression net for Burcin bug #5: <c>AddBurcinDatabaseDbContext</c> conditionally attaches
/// the <c>OutboxSavingChangesInterceptor</c> only when something has registered it. If a deployment
/// has Modules.Sourcing OFF (and no other module has wired reliable-messaging), the interceptor
/// is absent — and the DbContext registration must not throw.
///
/// Before the fix, <c>GetRequiredService</c> was used, throwing at host startup. The fix uses
/// <c>GetService</c> + null check.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class SourcingDeploymentTests
{
	[TestMethod]
	public async Task AddBurcinDatabaseDbContext_WithNoReliableMessagingRegistered_BuildsAndQueriesWithoutThrowing()
	{
		// Arrange — production AddBurcinDatabaseDbContext() registration, NO Sourcing module, NO Ruya
		// reliable-messaging chain. Mirrors a deployment where Modules.Sourcing feature flag is OFF.
		var services = new ServiceCollection();
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:MsSqlConnection"] = Initialize.Fixture.MsSqlConnectionString,
			})
			.Build();

		services.AddSingleton<IConfiguration>(config);
		services.AddLogging();
		services.AddOptions();

		// The actual production extension — this is what would otherwise throw on the missing interceptor.
		services.AddBurcinDatabaseDbContext();

		await using var sp = services.BuildServiceProvider(validateScopes: true);

		// Act — resolve and round-trip a query. If the interceptor wiring is broken (GetRequiredService),
		// service-provider construction itself wouldn't throw, but the first DbContext resolution would.
		await using var scope = sp.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<BurcinDatabaseDbContext>();

		// Hit the DB through the context to confirm it's actually configured (not metadata-only).
		var canConnect = await db.Database.CanConnectAsync();

		// Assert
		Assert.IsTrue(canConnect, "Sourcing-OFF deployment must still produce a usable DbContext.");
	}
}
