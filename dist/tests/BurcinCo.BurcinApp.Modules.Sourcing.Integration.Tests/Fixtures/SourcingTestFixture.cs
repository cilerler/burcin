using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using BurcinCo.BurcinApp.Data;
using BurcinCo.BurcinApp.Modules.Sourcing.Extensions;
using Ruya.Services.MessageQueue.Extensions;
using Ruya.Services.MessageQueue.RabbitMq;
using Ruya.Services.ReliableMessaging.Extensions;
using Ruya.Services.ReliableMessaging.MessageQueue.Extensions;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;

namespace BurcinCo.BurcinApp.Modules.Sourcing.Integration.Tests.Fixtures;

/// <summary>
/// Shared per-assembly fixture: spins up MsSql + RabbitMQ Testcontainers once for the whole test run,
/// applies the Sourcing schema via EF migrations, and exposes two test entry points:
///   <list type="bullet">
///     <item><see cref="CreateScope"/> — lightweight per-test DI scope (no host, no workers, no broker traffic)
///       used by tests that exercise the producer chain only.</item>
///     <item><see cref="BuildHostAsync"/> — full <see cref="IHost"/> with Outbox processor + RabbitMQ +
///       Sourcing workers; used by end-to-end tests that need to observe the broker round-trip.</item>
///   </list>
/// </summary>
internal sealed class SourcingTestFixture : IAsyncDisposable
{
	private readonly MsSqlContainer _mssql;
	private readonly RabbitMqContainer _rabbit;
	private ServiceProvider? _root;
	private bool _initialized;

	public SourcingTestFixture()
	{
		_mssql = new MsSqlBuilder("cilerler/mssql-server-linux:2025-RTM-ubuntu-22.04")
			.WithPassword("YourStrong!Passw0rd")
			.Build();

		// Default RabbitMQ image is enough — we don't exercise shovel/delayed-message plugins in these tests.
		_rabbit = new RabbitMqBuilder()
			.WithUsername("guest")
			.WithPassword("guest")
			.Build();
	}

	public string MsSqlConnectionString => _mssql.GetConnectionString();
	public string RabbitMqHost => _rabbit.Hostname;
	public int RabbitMqPort => _rabbit.GetMappedPublicPort(RabbitMqBuilder.RabbitMqPort);

	public async Task InitializeAsync()
	{
		if (_initialized) return;

		// Containers can come up in parallel — the only ordering constraint is that both must be up
		// before any test scope is created.
		await Task.WhenAll(_mssql.StartAsync(), _rabbit.StartAsync()).ConfigureAwait(false);
		_root = BuildRootServices();
		await EnsureSchemaAsync().ConfigureAwait(false);
		_initialized = true;
	}

	public async ValueTask DisposeAsync()
	{
		if (_root is not null)
		{
			await _root.DisposeAsync().ConfigureAwait(false);
		}
		await Task.WhenAll(_mssql.DisposeAsync().AsTask(), _rabbit.DisposeAsync().AsTask()).ConfigureAwait(false);
	}

	/// <summary>Create a per-test DI scope on the lightweight root SP. Dispose to release scoped instances.</summary>
	public AsyncServiceScope CreateScope()
	{
		if (_root is null) throw new InvalidOperationException("Fixture not initialized.");
		return _root.CreateAsyncScope();
	}

	/// <summary>Truncate Sourcing-owned tables between tests so each test starts from a clean slate.
	/// IngredientQuote is hard-delete (the soft-delete demo is on Recipe.Chef — single canonical
	/// example in the template). Outbox/Inbox are Ruya-owned and don't implement ISoftDelete either,
	/// so everything hard-deletes normally — no trigger disable/enable needed in this fixture.</summary>
	public async Task CleanTablesAsync()
	{
		await using var scope = CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<BurcinDatabaseDbContext>();

		// Outbox/Inbox stay in dbo (Ruya cross-cutting infrastructure); IngredientQuote moved to module schema.
		await db.Database.ExecuteSqlRawAsync("DELETE FROM dbo.Outbox").ConfigureAwait(false);
		await db.Database.ExecuteSqlRawAsync("DELETE FROM dbo.Inbox").ConfigureAwait(false);
		await db.Database.ExecuteSqlRawAsync("DELETE FROM Sourcing.IngredientQuote").ConfigureAwait(false);
	}

	/// <summary>
	/// Build + start a full <see cref="IHost"/> mirroring the production wire-up for the Sourcing module:
	/// shared <see cref="BurcinDatabaseDbContext"/>, Outbox interceptor + processor, RabbitMQ provider pointed at
	/// the test container, and the Sourcing module's workers (<c>QuoteRequestDispatcher</c>,
	/// <c>QuoteResponseSubscriber</c>). The supplier-side HTTP boundary is replaced with the supplied
	/// stub handler so tests can shape supplier responses (200 OK, 5xx, transport failure) without a real endpoint.
	/// </summary>
	public async Task<IHost> BuildHostAsync(HttpMessageHandler supplierHandler, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(supplierHandler);

		var builder = Host.CreateApplicationBuilder();

		builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
		{
			["ConnectionStrings:MsSqlConnection"] = MsSqlConnectionString,

			["MessageQueue:DefaultProvider"] = "default",
			["MessageQueue:Providers:default:Type"] = "RabbitMQ",
			["MessageQueue:Providers:default:Enabled"] = "true",
			["MessageQueue:RabbitMQ:Host"] = RabbitMqHost,
			["MessageQueue:RabbitMQ:Port"] = RabbitMqPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
			["MessageQueue:RabbitMQ:VirtualHost"] = "/",
			["MessageQueue:RabbitMQ:Username"] = "guest",
			["MessageQueue:RabbitMQ:Password"] = "guest",

			// Sourcing module config: one configured supplier whose URL the stub handler intercepts.
			// SupplierWebhookClientSettings binds the `Clients` section, so the dict-property `Suppliers`
			// becomes `Clients:Suppliers:<key>`.
			["Modules:Sourcing:Procurement:IngredientSupply:MessageQueueProviderName"] = "default",
			["Modules:Sourcing:Procurement:IngredientSupply:Clients:Suppliers:test-supplier:Url"]
				= "http://supplier.test/quote",
		});

		builder.Services.AddBurcinDatabaseDbContext();

		builder.Services.AddMessageQueue(builder.Configuration)
			.AddRabbitMQ(builder.Configuration);

		// Data owns the persistence-side reliable-messaging wiring (EF stores + interceptor configurer);
		// Host owns the broker bridge (AddMessageQueueOutboundDispatcher). Same composition as production.
		builder.Services.AddReliableMessaging()
			.AddBurcinDatabaseReliableMessaging()
			.AddMessageQueueOutboundDispatcher();

		builder.Services.AddSourcingModule(builder.Configuration);

		// Replace the primary handler on the named HttpClient that SupplierWebhookClient resolves.
		// The Sourcing module already registered AddHttpClient<SupplierWebhookClient>(); calling
		// AddHttpClient(<same-name>) again returns the same builder, and ConfigurePrimaryHttpMessageHandler
		// supplies the bottom of the handler chain. The class is internal to Sourcing, so we hard-code
		// the name here — kept in lockstep with the production wiring.
		builder.Services.AddHttpClient("SupplierWebhookClient")
			.ConfigurePrimaryHttpMessageHandler(_ => supplierHandler);

		var host = builder.Build();
		await host.StartAsync(cancellationToken).ConfigureAwait(false);
		return host;
	}

	/// <summary>Poll <paramref name="probe"/> on a fresh DI scope every 250 ms until it returns true or the timeout elapses.</summary>
	public async Task WaitUntilAsync(IHost host, Func<IServiceProvider, Task<bool>> probe, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(host);
		ArgumentNullException.ThrowIfNull(probe);

		var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
		while (DateTime.UtcNow < deadline)
		{
			cancellationToken.ThrowIfCancellationRequested();
			await using var scope = host.Services.CreateAsyncScope();
			if (await probe(scope.ServiceProvider).ConfigureAwait(false))
			{
				return;
			}
			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}
		throw new TimeoutException($"Probe did not become true within {timeout ?? TimeSpan.FromSeconds(30)}.");
	}

	private ServiceProvider BuildRootServices()
	{
		var services = new ServiceCollection();

		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:MsSqlConnection"] = MsSqlConnectionString,
			})
			.Build();

		services.AddSingleton<IConfiguration>(config);
		services.AddLogging(b => b.AddConfiguration(config.GetSection("Logging")));
		services.AddOptions();

		// Production-equivalent DbContext registration. The Outbox interceptor wiring happens
		// automatically via the IDbContextConfigurer<> seam once AddBurcinDatabaseReliableMessaging
		// registers its configurer (below). MigrationsAssemblyName pinning is required because
		// EnsureSchemaAsync calls MigrateAsync against a Testcontainer.
		services.AddBurcinDatabaseDbContext(s => s.MigrationsAssemblyName = "BurcinCo.BurcinApp.Migrations");

		// Reliable-messaging composition root + Data's per-context outbox/inbox + EF stores +
		// interceptor configurer. Skip AddMessageQueueOutboundDispatcher — that drains to a broker;
		// the lightweight scope doesn't need it.
		services.AddReliableMessaging()
			.AddBurcinDatabaseReliableMessaging();

		// Sourcing module DI. Registers ISourcingService + workers (workers won't run; no IHost.StartAsync).
		services.AddSourcingModule(config);

		return services.BuildServiceProvider(validateScopes: true);
	}

	private async Task EnsureSchemaAsync()
	{
		await using var scope = CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<BurcinDatabaseDbContext>();
		await db.Database.MigrateAsync().ConfigureAwait(false);

		// Apply post-migration soft-delete triggers so tests run against production-equivalent DB behavior.
		var triggersSqlPath = Path.Combine(AppContext.BaseDirectory, "triggers.sql");
		var triggersSql = await File.ReadAllTextAsync(triggersSqlPath).ConfigureAwait(false);
		var result = await _mssql.ExecScriptAsync(triggersSql).ConfigureAwait(false);
		if (result.ExitCode != 0)
		{
			throw new InvalidOperationException($"triggers.sql apply failed (exit {result.ExitCode}): {result.Stderr}");
		}
	}
}

/// <summary>
/// Test-side <see cref="HttpMessageHandler"/> stub that returns a configurable response and records every request.
/// </summary>
internal sealed class StubSupplierHandler : HttpMessageHandler
{
	private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
	private readonly List<HttpRequestMessage> _requests = new();

	public StubSupplierHandler(HttpStatusCode statusCode)
		: this(_ => new HttpResponseMessage(statusCode)) { }

	public StubSupplierHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
	{
		_responder = responder ?? throw new ArgumentNullException(nameof(responder));
	}

	public IReadOnlyList<HttpRequestMessage> ReceivedRequests => _requests;

	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		_requests.Add(request);
		return Task.FromResult(_responder(request));
	}
}
