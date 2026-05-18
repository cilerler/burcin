# ADR: Modular Polylith architecture for Burcin

- **Status:** Accepted
- **Date:** 2026-04-28
- **Authors:** Cengiz Ilerler

## Context

Burcin is a .NET 10 project template (Aspire-orchestrated, single deployable image, Yarp-fronted) that ships an opinionated shape for new line-of-business apps. The runtime topology we want is **one Docker image deployed to many independent k8s Deployments**, with each Deployment activating only its assigned module via `Microsoft.FeatureManagement` flags. This is neither a classical modular monolith (one process) nor classical microservices (separate codebases + databases) — and the gap between those two off-the-shelf patterns is exactly what this ADR fills.

Three forces shape the architecture:

1. **DB-first scaffolding wants one DbContext.** `dotnet ef dbcontext scaffold` produces one DbContext at a time, reading the whole DB. Splitting a single DB into multiple DbContexts requires per-schema scaffold runs (each manually filtered) or hand-curated entity moves on every schema change. For a one-person template that's perpetual friction.
2. **Cross-module reads via local snapshots are overkill at our scale.** Maintaining read-model projections (e.g. a `RecipeSnapshot` table inside Nutrition kept fresh by `RecipeChangedEvent` flowing over Outbox→broker→Inbox) is the canonical CQRS solution at high scale, but pays heavy storage + sync cost up front. With a 1-person team and a single physical SQL Server, the cost outweighs the benefit until proven otherwise.
3. **The deployment topology is "single binary, multiple roles."** The same Docker image is published to `recipe`, `nutrition`, `sourcing`, and `import` k8s Deployments — each Deployment activates only its assigned module. Cross-module communication uses HTTP/RPC when modules deploy separately and in-process method calls when they co-deploy.

The shape that fits: source-level modular separation, single physical database, per-deployment runtime activation. Internally we call it the **Modular Polylith** — modular at build, polylithic at runtime. Closest existing names in literature are "Single Binary, Multiple Roles" (HashiCorp/CockroachDB), "Service-Based Architecture" (Mark Richards), and "shared-database microservices" (informal). None capture the full shape; the name is internal shorthand.

## Decision

Adopt the **Modular Polylith**. Specifics:

### Project layout

```
src/
├─ <Org>.<App>.SharedKernel/                     # cross-cutting primitives (when used)
├─ <Org>.<App>.Models/                           # DB-first entity classes (Models/BurcinDatabase + BurcinDatabaseExtend) + Abstractions/ marker interfaces + BurcinDatabaseConstants/ enums
├─ <Org>.<App>.Data/                             # SHARED BurcinDatabaseDbContext (one for the whole app)
├─ <Org>.<App>.Migrations/                       # SINGLE migrations csproj; manually-run via tools/EntityFramework/migrate.ps1
├─ <Org>.<App>.Modules.<Module>.Abstractions/    # SIBLING csproj — module's PUBLIC cross-module contract (Interfaces, Events, Models, Requests, Responses). Dep-free; consumers reference ONLY this csproj, never the implementation.
│   ├─ Interfaces/I<Module>Service.cs
│   ├─ Events/, Models/, Requests/, Responses/
├─ <Org>.<App>.Modules.<Module>/                 # implementation csproj, follows app→module→component→service convention. ProjectReferences its own .Abstractions sibling.
│   ├─ Constants.cs                              # module-wide identifiers + FeatureFlag string
│   ├─ Extensions/StartupExtensions.cs           # Add<Module>Module + Map<Module>Module
│   └─ <Component>/                              # always at least one (per dotnet-service-generator skill)
│       ├─ Constants.cs, Extensions/StartupExtensions.cs
│       └─ <Service>/                            # one folder per service
│           ├─ Constants.cs                      # service-wide Metrics/Activities/Tags
│           ├─ Configuration/<Service>Settings.cs
│           ├─ Contracts/I<Service>.cs           # internal DI interface (default internal)
│           ├─ Extensions/StartupExtensions.cs   # Add<Service> + Map<Service>Api
│           ├─ Api/<Service>Api.cs               # MapGroup + per-verb handlers
│           ├─ Clients/                          # external API wrappers (incl. sibling modules treated as external)
│           ├─ Workers/                          # BackgroundService background work (Outbox dispatch, Inbox subscribe)
│           ├─ Handlers/                         # Inbox-deduped event handlers
│           └─ <Service>Service.cs               # the implementation
├─ <Org>.<App>.Host/                             # composition root; refs every module
├─ <Org>.<App>.Gateway/                          # YARP edge + webhook→broker translator
└─ <Org>.<App>.AppHost/                          # Aspire orchestrator
```

Reference modules in this template: `Modules.Recipe` (domain — Catalog component, Recipe/Chef/Category services), `Modules.Nutrition` (consumer — Tracking component, NutritionFact service), `Modules.Sourcing` (external integration demo — Procurement component, IngredientSupply service with both producer-via-Outbox and consumer-via-Inbox flows).

### Key rules

1. **Single shared `BurcinDatabaseDbContext`.** All modules read/write through the same context, registered in `<Org>.<App>.Data`. Entities live in `Models/BurcinDatabase/`. DB-first scaffolding becomes `dotnet ef dbcontext scaffold` — one command, regenerates every entity, all in one project.

   **Module = pair of csprojs.** Each module ships as two assemblies:
   - `Modules.<X>.Abstractions.csproj` — dep-free public contract (`Interfaces/`, `Events/`, `Models/`, `Requests/`, `Responses/`). The only surface other modules see.
   - `Modules.<X>.csproj` — implementation (components, services, controllers, workers, internal types). ProjectReferences its own `.Abstractions` sibling so it can implement the contract.

   A consuming module's csproj ProjectReferences ONLY the producing module's `.Abstractions`, never the implementation. The compiler physically blocks reach-in. When a `Modules.<X>.Abstractions` is empty (no public surface yet), the csproj doesn't need to exist — create it the moment a public type is first needed. The pattern only pays its keep when there's a contract to enforce.
2. **Single migrations project.** `<Org>.<App>.Migrations.csproj` is the migration target for all schema changes, applied manually via `tools/EntityFramework/migrate.ps1`.
3. **Module write boundary enforced at the SQL-permission level (production).** In production, each module deploys with its own SQL login (`recipe_user`, `nutrition_user`, …) granted broad SELECT but narrow INSERT/UPDATE/DELETE — its own schema only. Cross-module writes that violate this throw at the database. In the template default the connection uses one privileged user; permission split is a deployment hardening step.
4. **Module-only writes by convention in code.** Even with shared DbContext, `Modules.X` services never write to `Modules.Y`'s tables. Cross-module writes go through `IY<Service>` (Y's public contract). The DI binding picks one of:
   - `Modules.Y.<Component>.<Service>.<Service>Service` when Y is active in this deployment (in-process method call), or
   - `Modules.X.<Component>.<Service>.Clients.YClient` when Y is in a separate deployment (HTTP call against Y's `/api/...` endpoints).
   The consuming code is identical in both cases.
5. **Cross-module reads via direct EF queries.** Reads JOIN across schemas freely (DB allows it). When eventually consistent reads are acceptable AND a specific read path measurably needs decoupling, swap to a local snapshot fed by events. Not the default.
6. **One Docker image, multiple k8s Deployments via feature flags.** The same image powers every deployment. Each Deployment overrides `appsettings`'s `FeatureManagement` section to enable only its assigned modules:
   ```json
   "FeatureManagement": {
     "Modules.Recipe":   true,    // recipe deployment
     "Modules.Nutrition": false,
     "Modules.Sourcing":  false
   }
   ```
   Host's `ProgramExtensionsCustom` wraps each `Add<Module>Module` and `Map<Module>Module` call in an `if (fm.GetValue<bool>("Modules.<X>"))`. Inactive modules don't register DI, don't map endpoints, don't run background workers.
7. **Sibling modules are external (per Conventions-Naming-Standards.md).** When `Modules.Y` is in a different Deployment, `Modules.X` calls it via HTTP through `Modules.X/<Component>/<Service>/Clients/YClient.cs`. The client implements `IYService` from `Modules.Y.Abstractions.csproj` over HTTP. Since `Modules.X` only ProjectReferences `Modules.Y.Abstractions` (never `Modules.Y` itself), the client physically cannot reach into Y's implementation — the in-process-vs-HTTP swap is purely an `IYService` binding choice in DI. Convention-located in the consuming module's `Clients/` folder (which is documented as "wraps external HTTP APIs"; sibling modules count as external).
8. **Inbound webhooks: External → Gateway → broker → Inbox-deduped handler.** External callers POST to `/webhooks/{path}`. Gateway authenticates via `WebhookSecretAuthFilter`, wraps the body in a `MessageEnvelope` (CamelCase, matches `Ruya.Services.MessageQueue`'s serializer), and publishes to the per-topic exchange `webhooks.{path-with-dots}`. The owning module's `BackgroundService` subscribes via `IMessageQueue.SubscribeWithInboxAsync<TMessage, TContext>(...)`, dedups via the Inbox table, and invokes the scoped `<Event>Handler`.
9. **Outbox for outbound events.** A module's service writes business state + outbox event in one transaction. `OutboxProcessor` polls and dispatches via `MessageQueueOutboundDispatcher` to RabbitMQ. A worker (or in our `Modules.Sourcing` reference, `QuoteRequestDispatcher`) consumes from the broker and acts (HTTP to external supplier, side-effect, etc.).
10. **Dead-letter exchange wired by default.** Every subscribed topic gets a paired `{topic}.dlx` exchange + `{topic}.dlq` queue auto-declared, with `x-dead-letter-exchange` set on the consumer queue. Poison messages (deserialization failures, unhandled exceptions) are rejected without requeue and routed to the DLQ for inspection. The full original body is preserved as the audit trail.

### Outbox/Inbox configuration

The Outbox/Inbox tables live on the shared DbContext (`dbo.Outbox`, `dbo.Inbox`). Per-module separation is via `consumerName` for Inbox dedup and `topic` for Outbox routing — not via per-module schemas.

**Ownership: Data, gated by the `Sample` template flag.** Outbox/Inbox is persistence infrastructure (the schema mutates the database; the SaveChanges interceptor mutates the database; the EF stores read/write the database) and so belongs to the Data project, not to any module. When the template is generated with `--Sample`, Data takes a `PackageReference` to `Ruya.Services.ReliableMessaging.EntityFrameworkCore`, registers Outbox/Inbox entity configurations in `BurcinDatabaseDbContext.OnModelCreatingPostActions`, and exposes `AddBurcinDatabaseReliableMessaging(this IReliableMessagingBuilder builder)` — an extension method that wires the EF stores + interceptor configurer + outbox health check onto Host's single `AddReliableMessaging()` call. When the template is generated without `--Sample`, Data is Ruya-free, the Sourcing reference module isn't generated, and the migration doesn't include Outbox/Inbox tables.

**The `Modules.Sourcing` reference module is now purely a *consumer*** of reliable-messaging: it injects `IOutboxPublisher<BurcinDatabaseDbContext>` from Ruya, publishes events, and subscribes via `SubscribeWithInbox`. Sourcing has zero knowledge of how Outbox/Inbox is wired into the DbContext. The layering consequence: any *other* module that wants to publish reliable events (a future `Modules.Recipe` feature, say) imports the same Ruya interfaces from Data's transitive surface — it never needs to reference Sourcing.

**The runtime SaveChanges-interceptor wiring stays opt-in** via the `IDbContextConfigurer<BurcinDatabaseDbContext>` seam Data exposes. `AddBurcinDatabaseReliableMessaging` registers an `OutboxInterceptorConfigurer` that, when resolved by `AddBurcinDatabaseDbContext`'s configurer loop, adds the interceptor to options. Test fixtures that exercise outbox flows (Sourcing) call both `AddBurcinDatabaseDbContext()` and `AddReliableMessaging().AddBurcinDatabaseReliableMessaging()`; fixtures that don't (Recipe, Nutrition) call only `AddBurcinDatabaseDbContext()` — they get the Outbox/Inbox schema (so the model matches the migration) but the interceptor isn't wired, so SaveChanges doesn't try to flush anything.

## Alternatives considered

### Option A — Per-module DbContexts (modular monolith with hard data isolation)

Each module owns its own `DbContext`, its own EF migrations csproj, its own outbox/inbox tables, and its own SQL schema. Cross-module reads go through `IXQueryService` Contracts; cross-module data sharing uses local snapshots populated from events.

- Pros: Compile-time module isolation. Per-module migrations independent.
- Cons: DB-first scaffolding fights it (one scaffold per module per schema change). Cross-module reads require local snapshots. `MSDTC` for atomic multi-module writes (or compensating saga complexity). Heavyweight for current scale.
- **Rejected** — operational tax compounds over time and hampers DB-first iteration.

### Option B — Pure microservices (separate codebases + databases)

Fully decompose. Each module is its own repo, its own DB, its own everything.

- Pros: Maximum isolation. Independent technology choices per service. Industry-aligned.
- Cons: Distributed-systems tax (auth, tracing, schema versioning, deploy orchestration) for a 1-person team operating one DB cluster. Cross-service queries become network calls everywhere.
- **Rejected** — premature distribution.

### Option C — Single-process modular monolith (folders inside Host, no module csprojs)

Modules as folders in `Host/`, one csproj total.

- Pros: Simplest. Fastest builds. Trivial cross-module access for the cases where you actually need it.
- Cons: Loses independent deployment + per-module scale (which is the entire reason we're pursuing this). `internal` is assembly-wide so boundary discipline is convention-only.
- **Rejected** — independent deployment is a stated requirement.

### Option D — Cross-module reads via projections by default (event-sourced read models)

Snapshot pattern (`RecipeSnapshot`, `UserSnapshot`, …) populated by Outbox-published events.

- Pros: Strict consumer isolation; survives a DB split later without code changes.
- Cons: Storage duplication (every consumer keeps copies). Sync churn (every write fans out). Eventual consistency across all reads. Massive complexity at our scale.
- **Rejected as default** — keep it as a tool for measured hot paths or future per-service DB splits, not the default.

## Consequences

### Positive

- **DB-first scaffolding stays simple.** One `dotnet ef dbcontext scaffold` regenerates every entity into one project.
- **Cross-module reads are free** — direct EF JOINs across schemas. Production permission split (read everywhere, write narrow) catches accidental writes; convention + code review catch the rest.
- **One image, many roles.** Same Docker image deploys to every k8s Deployment; `appsettings`'s `FeatureManagement` is the only thing that differs between roles.
- **Bulk imports work without HTTP-shipping data.** A coordinator module calls each domain module's `BulkCreateAsync` in process when co-deployed, or via blob-staging + HTTP triggers when split — without ever serializing 1M records over the wire.
- **Poison messages are not fatal.** DLX auto-wired, exceptions default to reject-no-requeue, full body preserved in DLQ for inspection.
- **Eventing is opt-in per module.** Modules that don't need Outbox/Inbox pay nothing for the infrastructure; modules that do (e.g. cross-cutting audit, external-system integration) chain it onto `IReliableMessagingBuilder`.

### Negative / costs

- **Module write boundaries are enforced at deploy time, not compile time.** With the shared DbContext, any `Modules.X` developer can technically write to any table. Code review + the production permission split catch it; the compiler does not.
- **Schema coupling for cross-module reads.** If `Modules.Recipe` renames a column, `Modules.Nutrition`'s read query breaks. The team that owns Recipe is responsible for not breaking consumers — same as any API contract.
- **Single physical DB is a single point of failure.** Operational simplicity + cost of one cluster is the trade. If a service's load profile diverges, splitting that schema to its own DB is a connection-string change for that one service.
- **Cross-module HTTP introduces real failure modes when modules deploy separately** — timeouts, retries, circuit breakers, distributed tracing. Mitigated by `Microsoft.Extensions.Http.Resilience` + structured logging + the Inbox/Outbox patterns for async paths.

### Open items

- **Per-module SQL users.** A bootstrap script that creates `recipe_user`, `nutrition_user`, etc. with the right GRANTs. Production deployments only — the template default uses a single privileged login. Pairs with the next item.
- **Per-module schemas.** Moving `Recipe.*`, `Nutrition.*`, `Sourcing.*` into named schemas so the SQL permission split has natural boundaries to enforce. The template currently keeps everything in `dbo` for simplicity.
- **Cross-module HTTP integration tests.** Sibling-module HTTP clients are wired and unit-testable in-process, but exercising them across distinct Aspire deployments needs separate test infrastructure.

## References

- Conventions: `~/Source/github/cilerler/cilerler.github.io.wiki/Conventions-Naming-Standards.md` — Opinionated Folder Structures section
- Skill: `~/.claude/skills/dotnet-service-generator` — drives the per-service folder shape
