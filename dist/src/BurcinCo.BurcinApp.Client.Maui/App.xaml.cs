using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace BurcinCo.BurcinApp.Client.Maui;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState) =>
		new(new MainPage()) { Title = "BurcinApp" };
}
