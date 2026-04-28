using Microsoft.Extensions.Logging;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Aspire.Hosting.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace BurcinCo.BurcinApp.AppHost.Tests;

[TestClass]
public class WebTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    [TestMethod]
    public async Task GetWebResourceRootReturnsOkStatusCodeAsync()
    {
        // Arrange
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.BurcinCo_BurcinApp_AppHost>(["--environment=Development"]);
        appHost.Services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);
            // Override the logging filters from the app's configuration
            logging.AddFilter(appHost.Environment.ApplicationName, LogLevel.Debug);
            logging.AddFilter("BurcinCo.", LogLevel.Debug);
        });
        appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddStandardResilienceHandler();
        });

        await using var app = await appHost.BuildAsync().WaitAsync(DefaultTimeout);
        await app.StartAsync().WaitAsync(DefaultTimeout);

        // Act
        var httpClient = app.CreateHttpClient("api-client");
        await app.ResourceNotifications.WaitForResourceHealthyAsync("api-client").WaitAsync(DefaultTimeout);
        var response = await httpClient.GetAsync("/odata/$metadata");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }
}
