# BurcinCo.BurcinApp

<!--#if (Sample) -->
A **Modular Polylith** built on .NET 10 — one server runtime image, multiple Kubernetes Deployments,
runtime activation per module via `Microsoft.FeatureManagement` flags. The same Host image runs every
module in dev (one process), and runs ONE module per pod in production.
<!--#else -->
A .NET 10 application scaffold with thin Host and Gateway composition projects plus Aspire local
orchestration. The reference business modules are intentionally omitted; add sibling projects under
`src/` and keep business logic, contracts, and data tooling out of `BurcinCo.BurcinApp.Host`.
<!--#endif -->

> Architecture rationale: [docs/adrs/](docs/adrs/) — start with the modular-polylith record.

> [!IMPORTANT]
> **First task in a new project: rewrite the modular-polylith ADR for _this_ app.**
> Its filename and `Date:` are stamped at generation time, but its **body still describes the
> template's reference modules**.
<!--#if (Sample) -->
> The generated example includes those modules, but the ADR still presents the template domain as
> though it were this application's final domain. Tailor it before treating it as project truth.
<!--#else -->
> Those modules are not generated in this configuration, so leaving the ADR untouched would claim
> architectural decisions about code that does not exist here.
<!--#endif -->
>
> Replace the module roster and every rule that names a module with this app's real modules and
> components. Leave the folder shape below `{Component}/` to the `solution-structure` skill
> rather than restating it; that is the split that keeps the ADR and the skill from drifting
> apart.
<!--#if (Sample) -->
> Keep the rules that are genuinely architectural — the module-pair boundary, feature-flag
> activation, the Gateway edge, and the outbox seam — when they remain decisions for this app.
<!--#else -->
> The reference module and reliable-messaging decisions are examples, not commitments in this
> configuration; add them to the ADR only when this app actually adopts those boundaries.
<!--#endif -->

## What's in the box

| Layer | Project | Notes |
|---|---|---|
| Composition | `BurcinCo.BurcinApp.Host` | Thin ASP.NET Core app-runner wrapper. Owns process/configuration/module composition only—no contracts, business logic, data tooling, or service implementations. |
| Composition | `BurcinCo.BurcinApp.Gateway` | YARP edge runner with default per-client rate limits, configurable CIDR safelists, and trusted-proxy handling. With `--Sample`, owns process-intrinsic Webhook authentication, validation, envelope translation, and broker handoff; owns no application/domain behavior. |
| Composition | `BurcinCo.BurcinApp.AppHost` | Aspire orchestration for local dev — brings up MsSql, Redis, RabbitMQ, the Host, and the Gateway, and models selected client runners; MAUI targets are explicit-start. |
<!--#if (ClientShared) -->
| Client UI | `BurcinCo.BurcinApp.Client.Shared` | Razor Class Library containing the reusable routes, layout, navigation, pages, and FluentUI surface shared by the selected client runners. It is not a process. |
<!--#endif -->
<!--#if (Web) -->
| Client runner | `BurcinCo.BurcinApp.Client.Web` | Blazor Web process and render-mode shell. AppHost orchestrates it as `client-web`, and the Gateway exposes it at `/portal`; its Dockerfile is independent from the Host image. |
<!--#endif -->
<!--#if (Maui) -->
| Client runner | `BurcinCo.BurcinApp.Client.Maui` | .NET MAUI Blazor Hybrid native shell. Aspire exposes explicit-start platform resources for local development; its app-local `wwwroot/index.html` bootstraps `BlazorWebView`, which maps `Client.Shared.Routes` directly. |
<!--#endif -->
<!--#if (EntityFrameworkScaffold) -->
| Persistence | `BurcinCo.BurcinApp.Models` | DB-first entities + persistence marker interfaces (`Abstractions/`) + DB-tied enums (`BurcinDatabaseConstants/`). |
| Persistence | `BurcinCo.BurcinApp.Data` | Shared `BurcinDatabaseDbContext`. |
| Persistence | `BurcinCo.BurcinApp.Migrations` | Single migrations project for all modules; owns the EF design-time factory and migration-only configuration. |
<!--#endif -->
<!--#if (Sample) -->
| Module | `BurcinCo.BurcinApp.Modules.Recipe` | Reference module: Catalog component → Recipe, Chef, Category, Tag, and RecipePhoto services. |
| Module | `BurcinCo.BurcinApp.Modules.Nutrition` | Reference module: Tracking component → NutritionFact service + cross-module call to Recipe (in-process or HTTP via `RecipeClient`). |
| Module | `BurcinCo.BurcinApp.Modules.Sourcing` | Reference module: Procurement component → IngredientSupply service. Demonstrates non-default-provider Outbox routing, an atomic Inbox consumer with post-commit business telemetry, and finite delayed retry → DLX via Ruya reliable-messaging. |
<!--#endif -->

<!--#if (EntityFrameworkScaffold) -->
### Persistence scaffold

<!--#if (Sample) -->
The reference modules use this database layout:

| Schema | Owner | Tables |
|---|---|---|
| `Recipe` | Modules.Recipe | Chef, Recipe, RecipeExpansion, CategoryCode, CategoryGroup, CategoryCodeGroupMapping |
| `Nutrition` | Modules.Nutrition | NutritionFact |
| `Sourcing` | Modules.Sourcing | IngredientQuote |
| `dbo` | Cross-cutting infrastructure | Outbox, Inbox, `__EFMigrationsHistory` |

Module-owned tables live in their own schema; per-deployment SQL users get broad SELECT and narrow
INSERT/UPDATE/DELETE on their module's schema only — module isolation enforced at the database tier.
<!--#else -->
`BurcinCo.BurcinApp.Models`, `BurcinCo.BurcinApp.Data`, and
`BurcinCo.BurcinApp.Migrations` provide the shared EF Core foundation. No reference business module
is generated; define the schemas, entities, and module ownership for this application before adding
its initial migration.
<!--#endif -->
<!--#endif -->

## Local development

### Prerequisites

- .NET 10 SDK
- Docker Desktop (for Aspire's containers)
<!--#if (Maui) -->
- .NET MAUI workload plus the SDK/toolchain required by each native target you build
<!--#endif -->

Ruya dependencies restore from NuGet packages by default in every build configuration. To develop
against a local Ruya checkout, copy `Directory.Build.local.props.example` to
`Directory.Build.local.props`, set `LibrariesRoot`, and leave that gitignored override local.

The repository also includes an inactive `nuget.config.example`. Copy it to `nuget.config` when the
solution needs repository-specific package sources. Host and Gateway container builds copy
`nuget.config*`, so Docker restore automatically uses the active file when it exists while remaining
buildable with only the example present.

### Run

The Aspire AppHost owns the lifecycle of `mssql`, `redis`, and `rabbitmq` containers — never start
or stop them manually with `docker` commands.

```pwsh
aspire start --apphost src/BurcinCo.BurcinApp.AppHost
```

The Aspire dashboard prints its URL at startup. From there you can see the Host, Gateway, broker
activity, OpenTelemetry traces, and structured logs.
<!--#if (Web) -->
The same AppHost starts the Web runner as `client-web` after the Gateway is ready. The Gateway resolves that
resource through Aspire service discovery and exposes it at `/portal`. Web and Shared remain separate
projects; Host does not serve or reference their UI.
<!--#endif -->
<!--#if (Maui) -->

The AppHost registers MAUI through the preview `Aspire.Hosting.Maui` integration. Its Windows, Mac Catalyst,
Android-emulator, and iOS-simulator targets appear as explicit-start resources in the dashboard and build on
demand after the Gateway is ready. Select the target supported by your development machine.

The MAUI platform resources use `ExcludeFromManifest()`, so they remain local-development resources instead
of Docker Compose services. Restore the workload and build or package the target platform explicitly; for
example, on Windows:

```pwsh
dotnet workload restore src/BurcinCo.BurcinApp.Client.Maui/BurcinCo.BurcinApp.Client.Maui.csproj
dotnet build src/BurcinCo.BurcinApp.Client.Maui/BurcinCo.BurcinApp.Client.Maui.csproj `
    --framework net10.0-windows10.0.19041.0
```
<!--#endif -->

### Gateway edge protections

The Gateway applies ASP.NET Core token-bucket rate limiting by client IP before proxy or Webhook work.
`gateway-proxy` protects both YARP routes with a 200-request burst and replenishes 50 tokens every five
seconds. The Sample Webhook uses the tighter `gateway-webhook` policy: a 30-request burst and ten tokens
every ten seconds. Both reject immediately with `429 Too Many Requests`, a Problem Details body, and
`Retry-After` when the limiter can calculate it. Tune `Gateway:RateLimiting` after load testing; these
limits are per Gateway replica, so strict cross-replica quotas still belong at the ingress, WAF, or API
management layer. Health and Prometheus endpoints are intentionally exempt from request limiting.

Named CIDR safelists are also wired into the proxy, Webhook, and `/metrics` endpoints. They default to
`Enabled: false` so a generated public application and its Prometheus scraper are not accidentally locked
out. To restrict a surface, add exact IP addresses or canonical IPv4/IPv6 CIDRs and then enable its policy:

```json
"Gateway": {
  "NetworkSecurity": {
    "IpSafelists": {
      "gateway-proxy-ip-safelist": {
        "Enabled": true,
        "AllowedNetworks": [ "10.20.0.0/16", "2001:db8:1234::/48" ]
      }
    }
  }
}
```

An enabled safelist rejects an unknown or disallowed client address with `403 Forbidden` and does not
reveal the configured networks. Empty enabled lists, invalid CIDRs, and universal `/0` networks fail at
startup. Individual IPs are accepted and normalized to `/32` or `/128`. Gateway protection policies are
captured once at startup, so restart the Gateway after changing them.

Direct Kestrel traffic uses the socket peer address. When the Gateway runs behind a reverse proxy, enable
`Gateway:NetworkSecurity:ForwardedHeaders` and configure a finite `ForwardLimit` plus at least one exact
IP in `KnownProxies` or canonical CIDR in `KnownNetworks`. The Gateway ignores forwarded headers by
default and rejects the trust-all `ASPNETCORE_FORWARDEDHEADERS_ENABLED` switch; never make authorization
or rate-limit decisions from an untrusted `X-Forwarded-For` header.

<!--#if (EntityFrameworkScaffold) -->
### Apply EF migrations

The Host does not own migration tooling or migrate at startup. The sibling Migrations project contains the
design-time factory and `appsettings.Migration.json`; use it as both the target and startup project for EF CLI
commands. The template keeps a known, disposable local-development connection string in that migration-only
file so the template and its generated acceptance fixture are immediately runnable. This is template scaffolding,
not the configuration policy for an adopted application: before treating a generated repository as a real project,
move the connection value to project user secrets or an environment secret and remove the checked-in
`ConnectionStrings` entry from `appsettings.Migration.json`. The factory loads project user secrets and then
environment variables after the JSON file, so either source overrides the template fallback. Shared and deployed
environments must provide `ConnectionStrings__MigrationConnection` through their secret provider.

```pwsh
# Optional local override of the template fallback:
dotnet user-secrets set "ConnectionStrings:MigrationConnection" "<local migration connection string>" `
    --project src/BurcinCo.BurcinApp.Migrations
dotnet ef migrations add InitialBurcinApp `
    --context BurcinDatabaseDbContext `
    --project src/BurcinCo.BurcinApp.Migrations `
    --startup-project src/BurcinCo.BurcinApp.Migrations `
    --no-build
dotnet ef database update `
    --context BurcinDatabaseDbContext `
    --project src/BurcinCo.BurcinApp.Migrations `
    --startup-project src/BurcinCo.BurcinApp.Migrations
```
<!--#endif -->

<!--#if (Sample) -->
### Per-deployment activation

`appsettings.Development.json` defaults all module flags to `true` so a single image runs everything
locally. In production, each Deployment overlay flips all flags off except its own:

```json
"FeatureManagement": {
    "Modules.Recipe": false,
    "Modules.Nutrition": true,
    "Modules.Sourcing": false
}
```

The Host binds these keys once to `Configuration/CapabilitySelection` before building the service
provider. Module registration, MVC controller discovery, and minimal-API mapping all consume that same
immutable snapshot; module/component/service registration extensions do not accept or re-read
`IConfiguration`.

When `Modules.Recipe` is OFF in this Deployment, `Modules.Nutrition`'s wiring resolves
`IRecipeService` to the HTTP-backed `RecipeClient` instead of the in-process implementation. Sibling
modules running in their own pods are reached through that HTTP path.
<!--#endif -->

## Tests

```pwsh
dotnet build BurcinCo.BurcinApp.slnx
# Tests run as native MTP executables (not via VSTest); each test project's exe is in artifacts/bin.
.\artifacts\bin\BurcinCo.BurcinApp.Host.Integration.Tests\debug\BurcinCo.BurcinApp.Host.Integration.Tests.exe
.\artifacts\bin\BurcinCo.BurcinApp.Gateway.Integration.Tests\debug\BurcinCo.BurcinApp.Gateway.Integration.Tests.exe
.\artifacts\bin\BurcinCo.BurcinApp.AppHost.E2E.Tests\debug\BurcinCo.BurcinApp.AppHost.E2E.Tests.exe
<!--#if (Web) -->
.\artifacts\bin\BurcinCo.BurcinApp.Client.Web.Integration.Tests\debug\BurcinCo.BurcinApp.Client.Web.Integration.Tests.exe
<!--#endif -->
<!--#if (Sample) -->
.\artifacts\bin\BurcinCo.BurcinApp.Modules.Recipe.Integration.Tests\debug\BurcinCo.BurcinApp.Modules.Recipe.Integration.Tests.exe
.\artifacts\bin\BurcinCo.BurcinApp.Modules.Nutrition.Integration.Tests\debug\BurcinCo.BurcinApp.Modules.Nutrition.Integration.Tests.exe
.\artifacts\bin\BurcinCo.BurcinApp.Modules.Sourcing.Integration.Tests\debug\BurcinCo.BurcinApp.Modules.Sourcing.Integration.Tests.exe
<!--#endif -->
```

Test projects use **MSTest 4** with the `Microsoft.Testing.Platform` runner. The Host integration suite uses
`WebApplicationFactory` for the authenticated identity projection, the Gateway integration suite uses it for
process health, rate limiting, CIDR safelists, and trusted forwarded-address handling, and the AppHost E2E
suite uses `Aspire.Hosting.Testing` for orchestration plus public Gateway-to-Host traversal.
<!--#if (Web) -->
The Client.Web integration suite renders the shared `/portal/` route through the Web shell. AppHost E2E
coverage verifies both the `client-web` liveness endpoint and Gateway-to-Web traversal through `/portal/`.
<!--#endif -->
<!--#if (Sample) -->
The module suites use **Testcontainers** for ephemeral MsSql + RabbitMQ instances. The Gateway suite also
tests its selected Webhook edge adapter, and the E2E suite adds the Sample's public OData and
AppHost-managed-resource paths.
<!--#endif -->
Every scenario belongs to one layer and is not repeated in another.
<!--#if (Sample) -->
Each module's test project is self-contained — no shared fixture project — so module deletion takes its tests
with it.

A pristine scaffold intentionally contains no EF migration classes. Until the initial migration is
generated, module integration fixtures and the Aspire E2E fixture create their test database schema from
the model with `EnsureCreatedAsync`; once migrations exist, those fixtures apply them with `MigrateAsync`.
Soft-delete triggers are installed after either schema-initialization path. Application runners still never
migrate a deployed database at startup; this initialization belongs exclusively to test fixtures.
<!--#endif -->

| Project | Flavor | Coverage |
|---|---|---|
| `BurcinCo.BurcinApp.Host.Integration.Tests` | Integration | Authenticated current-user projection with the external identity-provider boundary substituted. |
<!--#if (Web) -->
| `BurcinCo.BurcinApp.Client.Web.Integration.Tests` | Integration | Web shell startup and server-rendered delivery of the shared client surface under `/portal`. |
<!--#endif -->
<!--#if (Sample) -->
| `BurcinCo.BurcinApp.Gateway.Integration.Tests` | Integration | In-process health probes, YARP edge-protection policies, trusted client-address handling, and Webhook registration, translation, broker handoff, bounded-body, safelist, and rate-limit behavior. |
| `BurcinCo.BurcinApp.AppHost.E2E.Tests` | E2E | Runner orchestration, public Gateway→Host process endpoints, OData metadata and CRUD, signed URLs, and Sourcing request persistence. |
<!--#else -->
| `BurcinCo.BurcinApp.Gateway.Integration.Tests` | Integration | In-process liveness/readiness contracts plus YARP rate-limit and CIDR-safelist policy wiring. |
| `BurcinCo.BurcinApp.AppHost.E2E.Tests` | E2E | Runner orchestration and public Gateway→Host process endpoints. |
<!--#endif -->
<!--#if (Sample) -->
| `BurcinCo.BurcinApp.Modules.Recipe.Integration.Tests` | Integration | Recipe CRUD round-trip, FK-to-Chef, view projection. |
| `BurcinCo.BurcinApp.Modules.Nutrition.Integration.Tests` | Integration | Cross-module call: Recipe-not-found, Recipe-found-in-process, Recipe-found-over-HTTP via stubbed `RecipeClient`. |
| `BurcinCo.BurcinApp.Modules.Sourcing.Integration.Tests` | Integration | Producer atomic write, Outbox→broker→supplier round-trip, Inbox dedup, poison message → DLQ, case-insensitive deserialize, quote-response transition/replay/conflict regressions, Sourcing-OFF deployment regression. |
<!--#endif -->

## Adding a new module

1. Create `src/BurcinCo.BurcinApp.Modules.{ModuleName}/{ComponentName}/{ServiceName}/{ServiceName}Service.cs`.
<!--#if (EntityFrameworkScaffold) -->
1. Add `[Table(nameof({Entity}), Schema = Constants.{ModuleName}Schema)]` and a new schema constant in `Models/Constants.cs` when the module owns persisted entities.
1. Add each persisted entity's `DbSet` to `BurcinDatabaseDbContext`.
<!--#endif -->
1. Add the module key to `Host/Configuration/CapabilitySelection.cs`; use that captured property for both
   registration and the Host → Module → Component → Service endpoint cascade in
   `Host/ProgramExtensionsCustom.cs`. Do not pass `IConfiguration` into module registration extensions.
<!--#if (EntityFrameworkScaffold) -->
1. Generate a new EF migration: `dotnet ef migrations add Add{ModuleName}Module …`.
<!--#endif -->
1. Add a sibling test project under `tests/` for the module's public behavior.

## Generated by

The [Burcin template](https://github.com/cilerler/burcin) (`dotnet new burcin`).
