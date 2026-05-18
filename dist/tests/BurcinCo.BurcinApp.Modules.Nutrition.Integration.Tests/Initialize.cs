using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BurcinCo.BurcinApp.Modules.Nutrition.Integration.Tests.Fixtures;

namespace BurcinCo.BurcinApp.Modules.Nutrition.Integration.Tests;

[TestClass]
public static class Initialize
{
	internal static NutritionTestFixture Fixture { get; private set; } = null!;

	[AssemblyInitialize]
	public static async Task AssemblyInitializeAsync(TestContext _)
	{
		Fixture = new NutritionTestFixture();
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
