# BurcinCo.BurcinApp

A **Modular Polylith** built on .NET 10 — a single Docker image, multiple Kubernetes Deployments,
runtime activation per module via `Microsoft.FeatureManagement` flags. The same image runs every
module in dev (one process), and runs ONE module per pod in production.

> Architecture rationale: [docs/adrs/202604281940-modular-polylith-architecture.md](docs/adrs/202604281940-modular-polylith-architecture.md).

## What's in the box

| Layer | Project | Notes |
|---|---|---|
| Composition | `BurcinCo.BurcinApp.Host` | ASP.NET Core entry point. Activates modules based on feature flags. |
| Composition | `BurcinCo.BurcinApp.Gateway` | YARP edge proxy + webhook → broker translator. |
| Composition | `BurcinCo.BurcinApp.AppHost` | Aspire orchestration for local dev — brings up MsSql, Redis, RabbitMQ, the Host, and the Gateway. |
| Persistence | `BurcinCo.BurcinApp.Models` | DB-first entities + persistence marker interfaces (`Abstractions/`) + DB-tied enums (`BurcinDatabaseConstants/`). |
| Persistence | `BurcinCo.BurcinApp.Data` | Shared `BurcinDatabaseDbContext`. |
| Persistence | `BurcinCo.BurcinApp.Migrations` | Single migrations project for all modules. |
| Module | `BurcinCo.BurcinApp.Modules.Recipe` | Reference module: Catalog component → Recipe + Chef + Category services. |
| Module | `BurcinCo.BurcinApp.Modules.Nutrition` | Reference module: Tracking component → NutritionFact service + cross-module call to Recipe (in-process or HTTP via `RecipeClient`). |
| Module | `BurcinCo.BurcinApp.Modules.Sourcing` | Reference module: Procurement component → IngredientSupply service. Demonstrates the Outbox-producer + Inbox-consumer + DLX pattern via Ruya reliable-messaging. |

### Database layout

| Schema | Owner | Tables |
|---|---|---|
| `Recipe` | Modules.Recipe | Chef, Recipe, RecipeExpansion, CategoryCode, CategoryGroup, CategoryCodeGroupMapping |
| `Nutrition` | Modules.Nutrition | NutritionFact |
| `Sourcing` | Modules.Sourcing | IngredientQuote |
| `dbo` | Cross-cutting infrastructure | Outbox, Inbox, `__EFMigrationsHistory` |

Module-owned tables live in their own schema; per-deployment SQL users get broad SELECT and narrow
INSERT/UPDATE/DELETE on their module's schema only — module isolation enforced at the database tier.

## Local development

### Prerequisites

- .NET 10 SDK
- Docker Desktop (for Aspire's containers)

### Run

The Aspire AppHost owns the lifecycle of `mssql`, `redis`, and `rabbitmq` containers — never start
or stop them manually with `docker` commands.

```pwsh
dotnet run --project src/BurcinCo.BurcinApp.AppHost
```

The Aspire dashboard prints its URL at startup. From there you can see the Host, Gateway, broker
activity, OpenTelemetry traces, and structured logs.

### Apply EF migrations

The Host does not migrate at startup — migrations are applied via the EF CLI:

```pwsh
$cs = "Server=127.0.0.1,1433;Database=BurcinApp;User Id=sa;Password=PasswordAdmin1!;TrustServerCertificate=true;Encrypt=true"
dotnet ef migrations add InitialBurcinApp `
    --context BurcinDatabaseDbContext `
    --project src/BurcinCo.BurcinApp.Migrations `
    --startup-project src/BurcinCo.BurcinApp.Host `
    --no-build
dotnet ef database update `
    --context BurcinDatabaseDbContext `
    --project src/BurcinCo.BurcinApp.Migrations `
    --startup-project src/BurcinCo.BurcinApp.Host `
    --connection $cs
```

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

When `Modules.Recipe` is OFF in this Deployment, `Modules.Nutrition`'s wiring resolves
`IRecipeService` to the HTTP-backed `RecipeClient` instead of the in-process implementation. Sibling
modules running in their own pods are reached through that HTTP path.

## Tests

```pwsh
dotnet build BurcinCo.BurcinApp.slnx
# Tests run as native MTP executables (not via VSTest); each test project's exe is in artifacts/bin.
.\artifacts\bin\BurcinCo.BurcinApp.Modules.Recipe.Integration.Tests\debug\BurcinCo.BurcinApp.Modules.Recipe.Integration.Tests.exe
.\artifacts\bin\BurcinCo.BurcinApp.Modules.Nutrition.Integration.Tests\debug\BurcinCo.BurcinApp.Modules.Nutrition.Integration.Tests.exe
.\artifacts\bin\BurcinCo.BurcinApp.Modules.Sourcing.Integration.Tests\debug\BurcinCo.BurcinApp.Modules.Sourcing.Integration.Tests.exe
.\artifacts\bin\BurcinCo.BurcinApp.AppHost.E2E.Tests\debug\BurcinCo.BurcinApp.AppHost.E2E.Tests.exe
```

Test projects use **MSTest 4** with the `Microsoft.Testing.Platform` runner. The module suites use
**Testcontainers** for ephemeral MsSql + RabbitMQ instances; the E2E suite uses
`Aspire.Hosting.Testing` to spin up the whole distributed app. Each module's test project is
self-contained — no shared fixture project — so module deletion takes its tests with it.

| Project | Flavor | Coverage |
|---|---|---|
| `BurcinCo.BurcinApp.Modules.Recipe.Integration.Tests` | Integration | Recipe CRUD round-trip, FK-to-Chef, view projection. |
| `BurcinCo.BurcinApp.Modules.Nutrition.Integration.Tests` | Integration | Cross-module call: Recipe-not-found, Recipe-found-in-process, Recipe-found-over-HTTP via stubbed `RecipeClient`. |
| `BurcinCo.BurcinApp.Modules.Sourcing.Integration.Tests` | Integration | Producer atomic write, Outbox→broker→supplier round-trip, Inbox dedup, poison message → DLQ, case-insensitive deserialize, Sourcing-OFF deployment regression. |
| `BurcinCo.BurcinApp.AppHost.E2E.Tests` | E2E | Full Aspire spin-up over HTTP: OData CRUD + PATCH semantics, stale-ETag 412, non-DB entity set parity, bound function `Recipe.GetSummary`, signed-URL photo flow, Sourcing `RequestQuote` 202, health endpoint. |

## Adding a new module

1. Create `src/BurcinCo.BurcinApp.Modules.{ModuleName}/{ComponentName}/{ServiceName}/{ServiceName}Service.cs` (mirror Sourcing's shape).
2. Add `[Table(nameof({Entity}), Schema = Constants.{ModuleName}Schema)]` and a new schema constant in `Models/Constants.cs`.
3. Add the entity's `DbSet` to `BurcinDatabaseDbContext`.
4. Wire DI in `Modules.{ModuleName}/Extensions/StartupExtensions.cs` and call it from `Host/ProgramExtensionsCustom.cs` under a new feature flag.
5. Generate a new EF migration: `dotnet ef migrations add Add{ModuleName}Module …`.
6. Add a sibling test project at `tests/BurcinCo.BurcinApp.Modules.{ModuleName}.Tests/` mirroring the existing module tests' fixture pattern.

## Generated by

The [Burcin template](https://github.com/cilerler/burcin) (`dotnet new burcin`).
