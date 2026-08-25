using BurcinCo.BurcinApp.Client.Shared;
using BurcinCo.BurcinApp.Client.Web.Components;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.FluentUI.AspNetCore.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();
builder.Services.AddFluentUIComponents();
builder.Services
	.AddRazorComponents()
	.AddInteractiveServerComponents();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UsePathBase("/portal");

if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/error", createScopeForErrors: true);
	app.UseHsts();
}

if (!app.Environment.IsEnvironment("Testing"))
{
	app.UseHttpsRedirection();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();

var liveOptions = new HealthCheckOptions { Predicate = _ => false };
var readyOptions = new HealthCheckOptions { Predicate = _ => true };
var healthGroup = app.MapGroup("").AllowAnonymous();
healthGroup.MapHealthChecks("/healthz/live", liveOptions);
healthGroup.MapHealthChecks("/healthz/ready", readyOptions);
healthGroup.MapHealthChecks("/healthz/startup", readyOptions);

app.MapRazorComponents<App>()
	.AddInteractiveServerRenderMode()
	.AddAdditionalAssemblies(typeof(Routes).Assembly);

await app.RunAsync().ConfigureAwait(false);

/// <summary>
/// Public marker used by the Web runner's integration-test host.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Design",
	"CA1515:Consider making public types internal",
	Justification = "WebApplicationFactory in the sibling integration-test assembly requires a public entry-point marker.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Major Code Smell",
	"S1118:Utility classes should not have public constructors",
	Justification = "This partial type marks the compiler-generated top-level entry point; it is not a utility class.")]
public partial class Program
{
}
