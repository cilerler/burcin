using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace BurcinCo.BurcinApp.AppHost;

internal static class Program
{
	private static readonly string VolumeDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @".docker/volumes/mySolution");

	// ReSharper disable once InconsistentNaming
	public static async Task Main(string[] args)
	{

		var builder = DistributedApplication.CreateBuilder(args);
		var compose = builder.AddDockerComposeEnvironment("compose")
				.WithProperties(env =>
				{
					env.DefaultNetworkName = "myNetwork";
					//env.DefaultContainerRegistry = "ghcr.io/burcinco";
				})
				.ConfigureComposeFile(file =>
				{
					file.Name = "myProject";
				})
				.WithDashboard(dashboard =>
				{
					dashboard
							.WithContainerName("aspire")
							.WithForwardedHeaders(enabled: true)
							.WithEndpoint("http", ep => { ep.Port = 18888; ep.TargetPort = 18888; ep.IsProxied = false; ep.IsExternal = true; })
							.WithEndpoint("otlp-grpc", ep => { ep.Port = 18889; ep.TargetPort = 18889; ep.IsProxied = false; ep.IsExternal = true; })
							.WithEndpoint("otlp-http", ep => { ep.Port = 18890; ep.TargetPort = 18890; ep.IsProxied = false; ep.IsExternal = true; })
							.PublishAsDockerComposeService((resource, service) =>
							{
								service.Name = "aspire";
							});
				});

		#region Redis
		var redisPassword = builder.AddParameter("redis-password", secret: true);
		var cache = builder.AddRedis("redis", 6379, redisPassword)
		.WithContainerName("redis")
		.WithImageTag("8.2")
		.WithBindMount(source: Path.Combine(VolumeDirectory, "redis/data"), target: "/data")
		.WithLifetime(ContainerLifetime.Persistent)
		.WithEndpoint("tcp", ep => { ep.Port = 6379; ep.IsProxied = false; ep.IsExternal = true; })
		.PublishAsDockerComposeService((resource, service) =>
			{
				service.Name = "redis";
				service.Restart = "unless-stopped";

			});

		cache
		.WithRedisInsight(_ =>
		{
			_.WithManifestPublishingCallback(_ => Task.CompletedTask)
			.WithContainerName("redis-insight")
			.WithImage("redis/redisinsight:2.70")
			.WithBindMount(Path.Combine(VolumeDirectory, "redis-insight/data"), "/db", false)
			.WithEndpoint("http", ep => { ep.Port = 16379; ep.IsProxied = false; ep.IsExternal = true; })
			.WithLifetime(ContainerLifetime.Persistent)
				.PublishAsDockerComposeService((resource, service) =>
				{
					service.Name = "redis-insight";
					service.Restart = "unless-stopped";
				})
			//.WithImagePullPolicy(ImagePullPolicy.Always);
			.WaitFor(cache)
			.WithParentRelationship(cache);
		});
		#endregion

		#region RabbitMQ
		var rabbitmqUsername = builder.AddParameter("rabbitmq-username", secret: true);
		var rabbitmqPassword = builder.AddParameter("rabbitmq-password", secret: true);
		var queue = builder.AddRabbitMQ("rabbitmq", rabbitmqUsername, rabbitmqPassword, 5672)
		.WithContainerName("rabbitmq")
		.WithImageTag("4.2-management")
		.WithEnvironment("RABBITMQ_PLUGINS_DIR", "/opt/rabbitmq/plugins:/usr/lib/rabbitmq/plugins")
		.WithBindMount(Path.Combine(VolumeDirectory, "rabbitmq/mnesia"), "/usr/lib/rabbitmq/mnesia", false)
		.WithBindMount(Path.Combine(VolumeDirectory, "rabbitmq-plugins"), "/usr/lib/rabbitmq/plugins", true)
		.WithEndpoint("tcp", ep => { ep.Port = 5672; ep.IsProxied = false; ep.IsExternal = true; })
		.WithLifetime(ContainerLifetime.Persistent)
		.WithManagementPlugin(15672)
		.WithArgs("/bin/sh", "-c", "rabbitmq-plugins list && rabbitmq-plugins enable rabbitmq_shovel rabbitmq_shovel_management rabbitmq_delayed_message_exchange && rabbitmq-server")
		// # copy https://github.com/rabbitmq/rabbitmq-delayed-message-exchange/releases/download/v4.2.0/rabbitmq_delayed_message_exchange-4.2.0.ez
		.PublishAsDockerComposeService((resource, service) =>
			{
				service.Name = "rabbitmq";
				service.Restart = "unless-stopped";
				service.Hostname = "rabbit-1";
			});

		foreach (var endpoint in queue.Resource.GetEndpoints())
		{
			if (endpoint.EndpointName == "management")
			{
				queue.WithEndpoint(endpoint.EndpointName, ep =>
				{
					ep.IsExternal = true;
					ep.IsProxied = false;
					ep.Port = 15672;
				});
			}
		}
		#endregion

		#region MSSQL
		var mssqlPassword = builder.AddParameter("mssql-password", secret: true);
		var databaseServer = builder.AddSqlServer("mssql", mssqlPassword, 1433)
							.WithContainerName("mssql")
							.WithImageRegistry("")
							.WithImage("cilerler/mssql-server-linux")
							.WithImageTag("2022-CU22-ubuntu-22.04")
							.WithDataBindMount(Path.Combine(VolumeDirectory, "mssql"))
							.WithEndpoint("tcp", ep => { ep.Port = 1433; ep.IsProxied = false; ep.IsExternal = true; })
							.WithLifetime(ContainerLifetime.Persistent)
							// .WithEnvironment("ACCEPT_EULA", "Y!")
							// .WithEnvironment("MSSQL_SA_PASSWORD", "PasswordAdmin1!")
							.WithEnvironment("MSSQL_PID", "Developer")
							.WithEnvironment("MSSQL_AGENT_ENABLED", "true")
							.WithEnvironment("MSSQL_ENABLE_HADR", "0")
							.PublishAsDockerComposeService((resource, service) =>
								{
									service.Name = "mssql";
									service.Restart = "unless-stopped";
									//service.User = "root";
									service.ExtraHosts = new Dictionary<string, string>
									{
										["production.database.windows.net"] = "192.0.2.1",
										["staging.database.windows.net"] = "192.0.2.2",
										["testing.database.windows.net"] = "192.0.2.3",
										["integration.database.windows.net"] = "192.0.2.4",
									};
								});
		var database = databaseServer.AddDatabase("BurcinDatabase");
		#endregion

		#region Durable Task Framework Monitor
		var dfmUsername = builder.AddParameter("dfm-username", secret: true);
		var dfmPassword = builder.AddParameter("dfm-password", secret: true);
		builder.AddContainer("dfm", "scaletone/durablefunctionsmonitor.mssql", "6.7")
			.WithContainerName("dfm")
			.WithEndpoint("http", ep => { ep.Port = 7072; ep.TargetPort = 80; ep.IsProxied = false; ep.IsExternal = true; })
			.WithEnvironment("DFM_SQL_CONNECTION_STRING", ReferenceExpression.Create($"data source={databaseServer.Resource.PrimaryEndpoint};initial catalog=DurableTaskFramework;persist security info=True;user id={dfmUsername};password={dfmPassword};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;App=DurableFunctionsMonitor;"))
			.WithEnvironment("DFM_NONCE", "i_sure_know_what_i_am_doing")
			.WithLifetime(ContainerLifetime.Persistent)
			.WaitFor(databaseServer)
			.PublishAsDockerComposeService((resource, service) =>
			{
				service.Name = "dfm";
				service.Restart = "unless-stopped";
			});
		#endregion

		// var mycache = builder.AddConnectionString("cache");
		// var myqueue = builder.AddConnectionString("queue");
		// var mydatabase = builder.AddConnectionString("database");

		var host = builder.AddProject<Projects.BurcinCo_BurcinApp_Host>("host", "BurcinCo.BurcinApp.Host")
			.WithHttpHealthCheck("/healthz")
			.WithReference(cache, "RedisConnection")
			.WaitFor(cache)
			.WithReference(queue, "RabbitMqConnection")
			.WaitFor(queue)
			.WithReference(database, "MsSqlConnection")
			.WaitFor(database)
			.PublishAsDockerComposeService((resource, service) =>
			{
		 		service.Name = "burcinco.burcinapp.host";
				service.Restart = "unless-stopped";
			});

		var gateway = builder.AddProject<Projects.BurcinCo_BurcinApp_Gateway>("gateway", "BurcinCo.BurcinApp.Gateway")
			.WithEndpoint("http", ep => { ep.Port = 80; ep.TargetPort = 80; ep.IsProxied = false; ep.IsExternal = true; })
			.WithEndpoint("https", ep => { ep.Port = 443; ep.TargetPort = 443; ep.IsProxied = false; ep.IsExternal = true; })
			.WithReference(host)
			// Note: do NOT inject ReverseProxy__Clusters__*__Destinations__*__Address here.
			// The Gateway resolves destinations via Microsoft.Extensions.ServiceDiscovery. WithReference(host)
			// automatically provides `services__host__http__0` in both Aspire local-dev and aspire-publish compose.
#if (Sample)
			.WithEnvironment(
				"ConnectionStrings__RabbitMqManagement",
				ReferenceExpression.Create($"http://{rabbitmqUsername}:{rabbitmqPassword}@{queue.Resource.PrimaryEndpoint.Property(EndpointProperty.Host)}:15672"))
#endif
			.PublishAsDockerComposeService((resource, service) =>
			{
				service.Name = "burcinco.burcinapp.gateway";
				service.Restart = "unless-stopped";
			});

		await builder.Build().RunAsync().ConfigureAwait(false);
	}
}
