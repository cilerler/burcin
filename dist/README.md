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

## Documentation

Use this README to get the repository running, then follow the canonical document for the question you have:

| When you need to… | Start here |
|---|---|
| Understand the selected projects, process boundaries, request flows, data ownership, or limitations | [System architecture](docs/architectures/system.md) |
| Configure Gateway rate limits, CIDR safelists, or trusted forwarded headers | [Gateway edge-protection SOP](docs/sops/configure-gateway-edge-protections.md) |
| Understand why an architectural choice was made | [Architecture decision records](docs/adrs/) |
| Add or find any other documentation | [Documentation index](docs/README.md) |
| Run or verify the repository | [Local development](#local-development) and [Tests](#tests) below |

The authored Markdown under `docs/` remains the source of truth whether or not a documentation site is
generated.

<!--#if (DocFx) -->
This repository also includes a searchable DocFX site and curated managed .NET API reference. Build, preview,
output, and optional GitHub Pages instructions live in the documentation index.
<!--#endif -->

## System at a glance

`BurcinCo.BurcinApp.Gateway` is the public edge, `BurcinCo.BurcinApp.Host` is the application runtime,
and `BurcinCo.BurcinApp.AppHost` orchestrates resources without joining the request path. The living
[system architecture](docs/architectures/system.md) is the canonical description of component ownership,
runtime paths, persistence, cross-cutting concerns, and known limitations. Its
[component inventory](docs/architectures/system.md#components) is the application-project map for this generated
repository; the [test inventory](#tests) maps verification projects separately.

## Local development

### Prerequisites

Choose one development environment:

- **Dev Container (recommended):** Git, Docker Desktop, Visual Studio Code, and its Dev Containers extension.
  The container supplies .NET 10, PowerShell, the Aspire CLI, VS Code's Aspire tooling, and an isolated Docker
  daemon for AppHost resources.
- **Host-native:** Git, .NET 10 SDK, PowerShell 7, the Aspire CLI, and Docker Desktop.

The Dev Container requires at least 8 CPUs, 32 GB of memory, and 64 GB of storage. From Visual Studio Code,
run **Dev Containers: Reopen in Container**. Initial setup restores the AppHost, restores optional repository
tools, and installs the pinned RabbitMQ delayed-message plugin used by the queue. AppHost data, Aspire parameter
secrets, and the HTTPS development certificate persist in repository-isolated Docker volumes across container
rebuilds. Aspire discovers and forwards dashboard and resource ports automatically; the template does not pin
stale application ports in `devcontainer.json`.
<!--#if (Maui) -->

The Linux Dev Container supports the server, Web, and shared-client projects. Build and run native Windows,
Android, iOS, and Mac Catalyst targets on a compatible host with the corresponding .NET MAUI workload,
SDK or emulator, and platform toolchain.
<!--#endif -->

Ruya dependencies restore from NuGet packages by default in every build configuration. To develop
against a local Ruya checkout, copy `Directory.Build.local.props.example` to
`Directory.Build.local.props`, set `LibrariesRoot`, and leave that gitignored override local.

The repository also includes an inactive `nuget.config.example`. Copy it to `nuget.config` when the
solution needs repository-specific package sources. Host and Gateway container builds copy
`nuget.config*`, so Docker restore automatically uses the active file when it exists while remaining
buildable with only the example present.

### Git hooks

After Git has been initialized, the first AppHost or solution build configures the repository-local
`core.hooksPath` automatically. It also marks the hook executable on macOS and Linux. If the project was built
before Git was initialized, build the AppHost or solution once more. Verify the result from the generated
application root:

```pwsh
git config --get core.hooksPath
```

The result is normally `tools/git/hooks`. A project generated inside an existing repository includes its
repository-relative folder prefix. If another hook path is already configured, or the default `.git/hooks`
directory already contains a non-sample hook, the build preserves it and emits a warning instead of disabling
the owner's hooks. Chain the Burcin hook deliberately or, when no existing hook must be preserved, activate it
manually from the generated application root:

```pwsh
$gitRoot = git rev-parse --show-toplevel
$repositoryPrefix = git rev-parse --show-prefix
$hooksPath = "${repositoryPrefix}tools/git/hooks"
git config --local core.hooksPath $hooksPath
if (-not $IsWindows) { chmod +x (Join-Path $gitRoot "$hooksPath/pre-commit") }
```

The hook validates staged C# and PlantUML changes. It rejects partially staged files in those two categories so
an unstaged edit cannot affect the commit; stage the complete file or stash its unstaged changes and retry.
Diagram authoring and manual rendering are documented under [PlantUML diagrams](docs/README.md#plantuml-diagrams).

### Run

Visual Studio Code provides both orchestration and project-by-project debugging:

- **Aspire: Launch AppHost** starts the AppHost, dashboard, infrastructure, and resources configured for
  automatic startup. While using this mode, let the AppHost own the `mssql`, `redis`, and `rabbitmq` containers.
- **Individual: Gateway + Host** starts only the two server projects. When selected, separate Web and MAUI
  compounds add the corresponding client. Each checked-in project configuration can also be launched on its own.

The individual compounds do not start the AppHost or provision infrastructure. Make the Development
connection-string dependencies available first; the standalone profiles then use fixed local application ports
so the Gateway can reach the Host and selected Web client without Aspire service-discovery variables.

From a terminal, run the orchestrated mode with:

```pwsh
aspire start --apphost src/BurcinCo.BurcinApp.AppHost
```

The Aspire dashboard prints its URL at startup. From there you can see the Host, Gateway, broker
activity, OpenTelemetry traces, and structured logs.
<!--#if (Web) -->
The selected Web runner is available through the Gateway at `/portal`. See the
[Web portal request](docs/architectures/system.md#web-portal-request) for the process boundary and route behavior.
<!--#endif -->
<!--#if (Maui) -->

For the individual MAUI compound, choose a target supported by the current machine when the .NET MAUI
extension prompts. In orchestrated mode, start that target explicitly from the Aspire dashboard. See
[Native client startup](docs/architectures/system.md#native-client-startup) for its orchestration and publishing
boundary. Restore the workload and build or package the target platform explicitly; for example, on Windows:

```pwsh
dotnet workload restore src/BurcinCo.BurcinApp.Client.Maui/BurcinCo.BurcinApp.Client.Maui.csproj
dotnet build src/BurcinCo.BurcinApp.Client.Maui/BurcinCo.BurcinApp.Client.Maui.csproj `
    --framework net10.0-windows10.0.19041.0
```
<!--#endif -->

### Configure Gateway edge protections

Rate limiting is enabled by default; CIDR safelists and forwarded-header trust are opt-in. Follow the
[Gateway edge-protection SOP](docs/sops/configure-gateway-edge-protections.md) to change and verify those
settings. The [system security architecture](docs/architectures/system.md#security) explains their enforcement
order, trust model, and scaling boundary.

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
