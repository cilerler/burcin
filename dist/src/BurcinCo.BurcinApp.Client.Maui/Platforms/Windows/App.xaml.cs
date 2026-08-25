using Microsoft.Maui;
using Microsoft.Maui.Hosting;

namespace BurcinCo.BurcinApp.Client.Maui.WinUI;

public partial class App : MauiWinUIApplication
{
	public App()
	{
		InitializeComponent();
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
