using Foundation;

using Microsoft.Maui;
using Microsoft.Maui.Hosting;

namespace BurcinCo.BurcinApp.Client.Maui;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
