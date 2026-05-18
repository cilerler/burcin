using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BurcinCo.BurcinApp.Modules.Sourcing.Integration.Tests.Fixtures;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Integration.Tests;

/// <summary>
/// Assembly-wide setup/teardown. Owns the shared <see cref="SourcingTestFixture"/> so the
/// MsSql + RabbitMQ Testcontainers + schema are created once per test-run, not per test.
/// </summary>
[TestClass]
public static class Initialize
{
	internal static SourcingTestFixture Fixture { get; private set; } = null!;

	[AssemblyInitialize]
	public static async Task AssemblyInitializeAsync(TestContext _)
	{
		Fixture = new SourcingTestFixture();
		await Fixture.InitializeAsync().ConfigureAwait(false);
	}

	[AssemblyCleanup]
	public static async Task AssemblyCleanupAsync()
	{
		if (Fixture is not null)
		{
			await Fixture.DisposeAsync().ConfigureAwait(false);
		}
	}
}
