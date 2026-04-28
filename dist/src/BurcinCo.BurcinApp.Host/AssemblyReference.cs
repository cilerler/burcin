using System.Reflection;

namespace BurcinCo.BurcinApp.Host;

public static class AssemblyReference
{
	public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
