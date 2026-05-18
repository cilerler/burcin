using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BurcinCo.BurcinApp.Modules.Recipe.Integration.Tests.Fixtures;

namespace BurcinCo.BurcinApp.Modules.Recipe.Integration.Tests;

[TestClass]
public static class Initialize
{
	internal static RecipeTestFixture Fixture { get; private set; } = null!;

	[AssemblyInitialize]
	public static async Task AssemblyInitializeAsync(TestContext _)
	{
		Fixture = new RecipeTestFixture();
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
