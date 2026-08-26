# ADR: Modular Polylith architecture for BurcinCo.BurcinApp

- **Status:** Accepted
- **Date:** (document-date)
- **Authors:** Cengiz Ilerler

## Context

BurcinCo.BurcinApp is a .NET 10 line-of-business application (Aspire-orchestrated and YARP-fronted) built on an opinionated project template. The server-module topology we want is **one Host image deployed to many independent k8s Deployments**, with each Deployment activating only its assigned module via `Microsoft.FeatureManagement` flags. Selected Web and MAUI clients remain separate runners around one shared Razor UI library; they are not folded into that Host image. This is neither a classical modular monolith (one process) nor classical microservices (separate codebases + databases) — and the gap between those two off-the-shelf patterns is exactly what this ADR fills.

Three forces shape the architecture:

1. **DB-first scaffolding wants one DbContext.** `dotnet ef dbcontext scaffold` produces one DbContext at a time, reading the whole DB. Splitting a single DB into multiple DbContexts requires per-schema scaffold runs (each manually filtered) or hand-curated entity moves on every schema change. For a one-person template that's perpetual friction.
2. **Cross-module reads via local snapshots are overkill at our scale.** Maintaining read-model projections (e.g. a `RecipeSnapshot` table inside Nutrition kept fresh by `RecipeChangedEvent` flowing over Outbox→broker→Inbox) is the canonical CQRS solution at high scale, but pays heavy storage + sync cost up front. With a 1-person team and a single physical SQL Server, the cost outweighs the benefit until proven otherwise.
3. **The deployment topology is "single binary, multiple roles."** The same Docker image is published to `recipe`, `nutrition`, `sourcing`, and `import` k8s Deployments — each Deployment activates only its assigned module. Cross-module communication uses HTTP/RPC when modules deploy separately and in-process method calls when they co-deploy.

The shape that fits: source-level modular separation, single physical database, per-deployment runtime activation. Internally we call it the **Modular Polylith** — modular at build, polylithic at runtime. Closest existing names in literature are "Single Binary, Multiple Roles" (HashiCorp/CockroachDB), "Service-Based Architecture" (Mark Richards), and "shared-database microservices" (informal). None capture the full shape; the name is internal shorthand.

## Decision

Adopt the **Modular Polylith**. Specifics:

### Project layout

The tree below intentionally stops at project boundaries; it is not a shortened service layout or a second
structural convention. Module, component, and service folders use the repository's complete canonical
structure. Capability folders are created only when they contain selected behavior, but every generated
artifact uses the one full canonical form.

```text
src/
├─ BurcinCo.BurcinApp.Abstractions/                         # app-wide cross-project contracts, when needed
├─ BurcinCo.BurcinApp.Domain/                               # app-wide domain types and rules, when needed
├─ BurcinCo.BurcinApp.Extensions/                           # reusable technical helpers, when needed
├─ BurcinCo.BurcinApp.Models/                               # DB-first persistence entities and partial extensions
├─ BurcinCo.BurcinApp.Data/                                 # shared BurcinDatabaseDbContext and persistence infrastructure
├─ BurcinCo.BurcinApp.Migrations/                           # EF design-time factory/config and migrations
├─ BurcinCo.BurcinApp.Modules.{ModuleName}.Abstractions/    # producer-owned cross-module contracts, when needed
├─ BurcinCo.BurcinApp.Modules.{ModuleName}/                 # module implementation; components and services are folders
├─ BurcinCo.BurcinApp.Services.{ServiceName}.Abstractions/  # standalone-service contracts, when another project consumes them
<!--#if (ClientShared) -->
├─ BurcinCo.BurcinApp.Client.Shared/                        # reusable Razor UI shared by selected client runners
<!--#endif -->
<!--#if (Web) -->
├─ BurcinCo.BurcinApp.Client.Web/                           # independent Blazor Web runner
<!--#endif -->
<!--#if (Maui) -->
├─ BurcinCo.BurcinApp.Client.Maui/                          # independent MAUI Blazor Hybrid runner
<!--#endif -->
├─ BurcinCo.BurcinApp.Host/                                 # application composition/app-runner wrapper only
├─ BurcinCo.BurcinApp.Gateway/                              # YARP edge and process-specific edge adapters
│  └─ Webhook/                                              # process-intrinsic webhook-to-broker edge adapter
└─ BurcinCo.BurcinApp.AppHost/                              # Aspire orchestration declarations only
```

`BurcinCo.BurcinApp.Models`, `.Data`, and `.Migrations` are the application's DB-first persistence projects.
They do not replace the repository's responsibility boundaries: cross-project application contracts belong in
`.Abstractions`, app-wide domain types and rules belong in `.Domain`, and reusable technical helpers belong in
`.Extensions`. Host, Gateway, and AppHost never become owners of those artifacts.

Reference modules in this template: `Modules.Recipe` (domain — Catalog component, Recipe/Chef/Category/Tag/RecipePhoto services), `Modules.Nutrition` (consumer — Tracking component, NutritionFact service), `Modules.Sourcing` (external integration demo — Procurement component, IngredientSupply service with both producer-via-Outbox and consumer-via-Inbox flows).

### Key rules

1. **Single shared `BurcinDatabaseDbContext`.** All modules read/write through the same context, registered in `BurcinCo.BurcinApp.Data`. Entities live in `Models/BurcinDatabase/`. DB-first scaffolding becomes `dotnet ef dbcontext scaffold` — one command, regenerates every entity, all in one project.

   **A module has one implementation project and a sibling contract project when cross-module contracts exist.**
   - `BurcinCo.BurcinApp.Modules.{ModuleName}.Abstractions.csproj` — implementation-free producer-owned contracts under `Interfaces/`, `Events/`, `Models/`, `Requests/`, `Responses/`, and contract-owned serialization metadata when required.
   - `BurcinCo.BurcinApp.Modules.{ModuleName}.csproj` — implementation containing components, services, controllers, root subscribers, and internal types. It directly references its own `.Abstractions` sibling when that project exists.

   A consuming module directly references the producing module's `.Abstractions` project, never its implementation. Every contract project also directly references any broader abstractions assembly whose types appear in its signatures; no project relies on another project's transitive reference to close compilation. Do not create an abstractions project merely because a CLR type is `public`: create it when a real cross-module contract requires that boundary.
2. **Single migrations project.** `BurcinCo.BurcinApp.Migrations.csproj` owns the EF design-time factory and migration-only configuration, is the migration target/startup project for schema changes, and is applied manually via `tools/EntityFramework/migrate.ps1`. Host remains runtime composition-only.
3. **Module write boundary enforced at the SQL-permission level (production).** In production, each module deploys with its own SQL login (`recipe_user`, `nutrition_user`, …) granted broad SELECT but narrow INSERT/UPDATE/DELETE — its own schema only. Cross-module writes that violate this throw at the database. In the template default the connection uses one privileged user; permission split is a deployment hardening step.
4. **Module-only writes by convention in code.** Even with shared DbContext, `Modules.{ModuleName}` services never write to `Modules.{OtherModuleName}`'s tables. Cross-module writes go through the producer-owned `I{OtherServiceName}` contract from `BurcinCo.BurcinApp.Modules.{OtherModuleName}.Abstractions`. The DI binding picks one of:
   - `BurcinCo.BurcinApp.Modules.{OtherModuleName}.{OtherComponentName}.{OtherServiceName}.{OtherServiceName}Service` when the producer module is active in this deployment (in-process method call), or
   - `BurcinCo.BurcinApp.Modules.{ModuleName}.{ComponentName}.{ServiceName}.Clients.{OtherServiceName}Client` when the producer module is in a separate deployment (HTTP call against its `/api/...` endpoints).
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
   Host captures these keys once into an immutable `CapabilitySelection` before building the service provider. The same snapshot gates every `Add{ModuleName}Module` and `Map{ModuleName}Module` call. Inactive modules don't register DI, don't map endpoints, and don't run root subscribers; configuration reload cannot split the registration and mapping decisions within a process. Registration and mapping follow the same Host → Module → Component → Service cascade. An API-exposed service maps through the public `Map{ServiceName}(enabled)` wrapper, which verifies registration before calling its internal low-level `Map{ServiceName}Api()` mapper; no mapper re-reads live configuration.
7. **Sibling modules are external dependencies.** When `Modules.{OtherModuleName}` is in a different Deployment, the consuming service calls it through `Clients/{OtherServiceName}Client.cs`. That client implements `I{OtherServiceName}` from the producer module's `.Abstractions` project over HTTP. Because the consumer directly references only the producer's contract project—never its implementation—the in-process-versus-HTTP choice remains a DI binding decision and implementation reach-in is physically blocked.
8. **Inbound webhooks: External → Gateway Webhook edge adapter → broker → Inbox-deduplicated business service.** External callers POST to `/webhooks/{path}`. The Gateway-owned `Webhook` adapter authenticates and validates the request, applies the body limit, translates the envelope, and hands it to the broker; it contains no application/domain decisions. It wraps the body in the stable lower-camel `MessageEnvelope` shape and publishes to the per-topic exchange `webhooks.{path-with-dots}`. A root `{EventName}Subscriber` `BackgroundService` owns the queue and subscription lifetime and subscribes via `IMessageQueue.SubscribeWithInboxAndPostCommitAsync<TMessage, TContext>(...)`. The Inbox table deduplicates delivery; the thin subscriber adapter resolves the scoped `{ServiceName}Service` and delegates the payload. Business state mutates inside the atomic callback; committed-work logs and counters run only from the post-commit observer.
9. **Outbox for outbound events.** A module's service writes business state + outbox event in one transaction. Every envelope stamps the service's configured non-default dispatcher/provider name, and `OutboxProcessor` dispatches via `MessageQueueOutboundDispatcher` to RabbitMQ. A root subscriber (in the `Modules.Sourcing` reference, `IngredientQuoteRequestedEventSubscriber`) owns the subscription lifetime and thinly delegates the side effect to its scoped business service.
10. **Dead-letter exchange wired by default.** Every subscribed topic gets a paired `{topic}.dlx` exchange + `{topic}.dlq` queue auto-declared, with `x-dead-letter-exchange` set on the consumer queue. Poison messages (deserialization failures, unhandled exceptions) are rejected without requeue and routed to the DLQ for inspection. The full original body is preserved as the audit trail.

### Outbox/Inbox configuration

The Outbox/Inbox tables live on the shared DbContext (`dbo.Outbox`, `dbo.Inbox`). Per-module separation is via `consumerName` for Inbox dedup and `topic` for Outbox routing — not via per-module schemas.

**Ownership: Data, gated by the `Sample` template flag.** Outbox/Inbox is persistence infrastructure (the schema mutates the database; the SaveChanges interceptor mutates the database; the EF stores read/write the database) and so belongs to the Data project, not to any module. When the template is generated with `--Sample`, Data takes a `PackageReference` to `Ruya.Services.ReliableMessaging.EntityFrameworkCore`, registers Outbox/Inbox entity configurations in `BurcinDatabaseDbContext.OnModelCreatingPostActions`, and exposes `AddBurcinDatabaseReliableMessaging(this IReliableMessagingBuilder builder)` — an extension method that wires the EF stores + interceptor configurer + outbox health check onto Host's single `AddReliableMessaging()` call. When the template is generated without `--Sample`, Data is Ruya-free, the Sourcing reference module isn't generated, and the migration doesn't include Outbox/Inbox tables.

**The `Modules.Sourcing` reference module is now purely a *consumer*** of reliable-messaging: it injects `IOutboxPublisher<BurcinDatabaseDbContext>` from Ruya, publishes events, and subscribes via `SubscribeWithInbox`. Sourcing has zero knowledge of how Outbox/Inbox is wired into the DbContext. Any other module that publishes reliable events directly references the Ruya contract packages or their local-source project equivalents that its code compiles against. Data owns the persistence integration; it is not a transitive dependency surface for consumers, and no module needs to reference Sourcing.

**The runtime SaveChanges-interceptor wiring stays opt-in** via the `IDbContextConfigurer<BurcinDatabaseDbContext>` seam Data exposes. `AddBurcinDatabaseReliableMessaging` registers an `OutboxInterceptorConfigurer` that, when resolved by `AddBurcinDatabaseDbContext`'s configurer loop, adds the interceptor to options. Test fixtures that exercise outbox flows (Sourcing) call both `AddBurcinDatabaseDbContext()` and `AddReliableMessaging().AddBurcinDatabaseReliableMessaging()`; fixtures that don't (Recipe, Nutrition) call only `AddBurcinDatabaseDbContext()` — they get the Outbox/Inbox schema (so the model matches the migration) but the interceptor isn't wired, so SaveChanges doesn't try to flush anything.

### Sourcing quote-response transition matrix

Inbox deduplication suppresses repeated deliveries of one broker envelope ID. A supplier can still repeat the same business response under a fresh envelope ID, so `IngredientSupplyService` also enforces the persisted quote state as the business-idempotency boundary. The first response committed from `Sent` wins: a matching terminal replay is a successful no-op that preserves the first response, while an out-of-order response or a conflicting terminal outcome is permanently rejected without mutating the quote row.

| Current status | Incoming response | Result | Quote-row mutation | Executable coverage |
|---|---|---|---|---|
| `Pending` | Accepted | Permanently reject as out of order | None | `ProcessAsync_PendingAcceptedResponse_ThrowsAndDoesNotMutate` |
| `Pending` | Rejected | Permanently reject as out of order | None | `ProcessAsync_PendingRejectedResponse_ThrowsAndDoesNotMutate` |
| `Sent` | Accepted | Transition to `ResponseReceived` | Store response timestamp and payload; clear `FailureReason` | `ProcessAsync_SentAcceptedResponse_TransitionsToResponseReceived` |
| `Sent` | Rejected | Transition to `Failed` | Store response timestamp, payload, and rejection reason | `ProcessAsync_SentRejectedResponse_TransitionsToFailed` |
| `ResponseReceived` | Accepted | Successful business no-op | Preserve the first committed terminal response | `ProcessAsync_ResponseReceivedAcceptedResponseFromFreshEnvelope_IsNoOp` |
| `ResponseReceived` | Rejected | Permanently reject conflicting terminal outcome | None | `ProcessAsync_ResponseReceivedRejectedResponse_ThrowsAndDoesNotMutate` |
| `Failed` | Accepted | Permanently reject conflicting terminal outcome | None | `ProcessAsync_FailedAcceptedResponse_ThrowsAndDoesNotMutate` |
| `Failed` | Rejected | Successful business no-op | Preserve the first committed terminal failure | `ProcessAsync_FailedRejectedResponseFromFreshEnvelope_IsNoOp` |

All matrix cases live in `tests/BurcinCo.BurcinApp.Modules.Sourcing.Integration.Tests/IngredientSupplyService/ResponseStateTransitionTests.cs`. The direct service invocations intentionally bypass Inbox identity so the two replay cases model deliveries that carry distinct envelope IDs.

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

- Repository overview: root `README.md`
- Capability-selection snapshot: `src/BurcinCo.BurcinApp.Host/Configuration/CapabilitySelection.cs`
<!--#if (EntityFrameworkScaffold) -->
- EF migration workflow: `tools/EntityFramework/migrate.ps1`
<!--#endif -->
