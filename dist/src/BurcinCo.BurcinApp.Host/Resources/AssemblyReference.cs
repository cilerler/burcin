using System.Reflection;

namespace BurcinCo.BurcinApp.Host.Resources;

internal static class AssemblyReference
{
	public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
