using ObjCRuntime;
using UIKit;

namespace BurcinCo.BurcinApp.Client.Maui;

internal static class Program
{
	private static void Main(string[] args) =>
		UIApplication.Main(args, null, typeof(AppDelegate));
}
