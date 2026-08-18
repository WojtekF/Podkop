# Persistence patterns: EF Core + PostgreSQL in vertical slices

Resolves the wayfinder ticket "Persistence patterns: EF Core + Postgres in vertical slices" ([WojtekF/Podkop#56](https://github.com/WojtekF/Podkop/issues/56)). Research date: 2026-08-18.

Sources consulted (primary):

- Aspire docs (now hosted at aspire.dev; learn.microsoft.com Aspire URLs 301-redirect there): [EF Core migrations integration](https://aspire.dev/integrations/databases/efcore/migrations/), [PostgreSQL hosting integration](https://aspire.dev/integrations/databases/postgres/postgres-host/), [PostgreSQL EF Core client](https://aspire.dev/integrations/databases/efcore/postgres/postgresql-connect/), [testing tutorial](https://aspire.dev/testing/write-your-first-test/), [support policy](https://aspire.dev/support/)
- EF Core docs: [testing overview](https://learn.microsoft.com/en-us/ef/core/testing/), [testing with the database](https://learn.microsoft.com/en-us/ef/core/testing/testing-with-the-database), [data seeding](https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding), [migrations overview](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/), [applying migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying), [separate migrations project](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/projects), [custom migrations history table](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/history-table), [design-time DbContext creation](https://learn.microsoft.com/en-us/ef/core/cli/dbcontext-creation), [entity types / table schema](https://learn.microsoft.com/en-us/ef/core/modeling/entity-types), [transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions), [What's new in EF Core 10](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew)
- Milan Jovanović: [Modular Monolith Data Isolation](https://www.milanjovanovic.tech/blog/modular-monolith-data-isolation), [Using Multiple EF Core DbContexts In a Single Application](https://milanjovanovic.tech/blog/using-multiple-ef-core-dbcontext-in-single-application)
- [Testcontainers for .NET — PostgreSQL module](https://dotnet.testcontainers.org/modules/postgres/), [ASP.NET Core example](https://dotnet.testcontainers.org/examples/aspnet/)
- [Npgsql basic usage](https://www.npgsql.org/doc/basic-usage.html), NuGet pages for [Npgsql.EntityFrameworkCore.PostgreSQL](https://www.nuget.org/packages/Npgsql.EntityFrameworkCore.PostgreSQL), [Aspire.Npgsql.EntityFrameworkCore.PostgreSQL](https://www.nuget.org/packages/Aspire.Npgsql.EntityFrameworkCore.PostgreSQL), [Aspire.Hosting.EntityFrameworkCore](https://www.nuget.org/packages/Aspire.Hosting.EntityFrameworkCore), [Respawn](https://github.com/jbogard/respawn)

Access note: milanjovanovic.tech and its Medium mirror return HTTP 403 to automated fetchers. Claims from those two articles were verified through search-engine excerpts of the article pages themselves and are cited by canonical URL; they could not be re-read in full. Everything else was read directly.

## TL;DR

- **Current versions (verified 2026-08-18):** Aspire **13.4** (released 2026-06-01; Aspire now versions independently of .NET and only the latest release is supported — [support policy](https://aspire.dev/support/)). EF Core **10** (Nov 2025, LTS until 2028-11-10, requires .NET 10 — [What's new](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew)). Npgsql EF provider **10.0.3** ([NuGet](https://www.nuget.org/packages/Npgsql.EntityFrameworkCore.PostgreSQL)). EF Core 11 / provider 11 are in preview.
- **Schema-per-slice, one database** is the standard modular-monolith arrangement: one `DbContext` per slice, `HasDefaultSchema` + a per-schema `MigrationsHistoryTable`, migrations owned by each slice. Milan Jovanović's recommended starting level of data isolation ([data isolation article](https://www.milanjovanovic.tech/blog/modular-monolith-data-isolation)).
- **Migrations live next to each DbContext** (per-slice Infrastructure projects fit this naturally); a **dedicated migration-service worker** applies them at startup, gated by `WaitFor`/`WaitForCompletion` in the AppHost ([Aspire migrations doc](https://aspire.dev/integrations/databases/efcore/migrations/)). A newer `AddEFMigrations` AppHost API exists but its package is still prerelease.
- **Testing:** Microsoft explicitly discourages the EF InMemory provider; testing against real PostgreSQL via **Testcontainers + WebApplicationFactory** is the best fit for Podkop's existing endpoint tests, with **Respawn** for reset. `Aspire.Hosting.Testing` is a heavier full-orchestration alternative for a few smoke tests.
- **Dev/demo seed data** should NOT use `HasData` (that is "model managed data" for static reference data). Use the migration service's seeding step (or `UseSeeding` registered conditionally), gated on environment.

## 1. DbContext-per-slice vs shared DbContext

### The modular-monolith isolation ladder

Milan Jovanović's [Modular Monolith Data Isolation](https://www.milanjovanovic.tech/blog/modular-monolith-data-isolation) describes four isolation levels: separate **tables** (weakest), separate **schemas**, separate **databases**, and separate **persistence technology** (strongest). His recommended starting point is logical isolation with **one schema per module**, because it is cheap to implement and makes boundaries visible; separate databases buy stricter isolation at the cost of real operational complexity and are recommended only when strict isolation is genuinely required. Modules must follow rules regardless of level: a module accesses only its own tables, tables are not shared between modules, and no module queries another module's tables directly — enforced socially and via architecture tests.

Schema-per-module in EF Core means **one DbContext per module**, and his [multiple-DbContexts article](https://milanjovanovic.tech/blog/using-multiple-ef-core-dbcontext-in-single-application) names modular monoliths as a primary use case: each context is configured with a different default schema. Two consequences he calls out:

- You **cannot join across DbContexts** in a single LINQ query — EF Core does not know two contexts target the same database.
- **Transactions across contexts work only when the contexts use the same database** (see below for the EF mechanism).

### EF Core mechanics for multiple contexts on one database

- **`HasDefaultSchema`** — sets the model-wide schema so every table (and sequence) of that context lands in the slice's schema ([entity types doc](https://learn.microsoft.com/en-us/ef/core/modeling/entity-types#table-schema)).
- **`MigrationsHistoryTable`** — by default every context records applied migrations in `__EFMigrationsHistory`; the default schema does *not* apply to the history table, so each context must place its own history table into its own schema (or use a distinct name) to avoid collisions ([custom history table doc](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/history-table), corroborated by [Milan's article](https://milanjovanovic.tech/blog/using-multiple-ef-core-dbcontext-in-single-application)).
- **`MigrationsAssembly`** — points EF at the assembly holding a context's migrations when they are not in the startup assembly ([separate migrations project doc](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/projects)).
- **`ExcludeFromMigrations`** — when the same table must appear in more than one context's model (bounded-context style), one context maps it read-only and excludes it from its migrations so only one context owns the DDL ([migrations overview](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/#excluding-parts-of-your-model), [entity types doc](https://learn.microsoft.com/en-us/ef/core/modeling/entity-types#excluding-from-migrations)).

Combined shape (adapted from the EF docs above):

```csharp
// Features/Findings/Podkop.Findings.Infrastructure
services.AddDbContext<FindingsDbContext>(o => o.UseNpgsql(
    connectionString,
    npgsql => npgsql
        .MigrationsAssembly(typeof(FindingsDbContext).Assembly.GetName().Name)
        .MigrationsHistoryTable("__EFMigrationsHistory", "findings")));

// inside FindingsDbContext
protected override void OnModelCreating(ModelBuilder b)
    => b.HasDefaultSchema("findings");
```

### Trade-offs

| Concern | Shared single DbContext | DbContext per slice (schemas) | Database per slice |
| --- | --- | --- | --- |
| Slice isolation | None — every slice references one shared model; boundary erosion is one navigation property away | Strong at the model level; physical isolation still soft (same DB) — back it with architecture tests ([Milan](https://www.milanjovanovic.tech/blog/modular-monolith-data-isolation)) | Hard |
| Cross-slice queries | Trivial (joins) — which is exactly the leak vertical slices forbid | Not possible in one LINQ query across contexts ([Milan](https://milanjovanovic.tech/blog/using-multiple-ef-core-dbcontext-in-single-application)); go through the other slice's Application layer or Contracts | Only via APIs/events |
| Transactions | Single `SaveChanges` is already atomic | Cross-context atomicity possible: contexts can share one `DbConnection` and enlist via `Database.UseTransaction` (relational providers only) ([EF transactions doc](https://learn.microsoft.com/en-us/ef/core/saving/transactions#cross-context-transaction)) | Distributed transactions / sagas — avoid |
| Migrations | One migration stream; every slice change contends on it | One stream per slice, each with its own history table | One per database |
| Connection pooling | One pool | All contexts share one connection string; Npgsql pools physical connections by default ([Npgsql docs](https://www.npgsql.org/doc/basic-usage.html)). Inference: since pooling is keyed by connection string, N contexts do not multiply pools | One pool per database |
| Ops | One DB | One DB (schemas are free) | N databases to provision, back up, migrate |

Inference for Podkop: the per-slice `Podkop.<Feature>.Infrastructure` projects already exist, so DbContext-per-slice adds no new projects — it is the arrangement the repo's structure has been anticipating. A shared DbContext would require a new shared persistence project that every slice references, violating the "features don't reference each other's internals" rule at the persistence layer.

## 2. Where migrations live and run in an Aspire solution

### Where migrations live

The Aspire migrations doc is explicit that migrations belong in "the project that contains your Entity Framework `DbContext` and model classes" — the migration service only applies them ([Aspire migrations doc](https://aspire.dev/integrations/databases/efcore/migrations/)). For Podkop that is each slice's Infrastructure project. The EF docs' [separate-migrations-project layout](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/projects) (Data / Migrations / App triple) exists for platform-specific startup projects and for keeping multiple migration sets; with per-slice Infrastructure projects that separation already exists relative to `Podkop.Server`, so a fourth "Migrations" project per slice is not required (Inference).

### Design-time context discovery

EF tools create the context at design time in this priority order ([design-time creation doc](https://learn.microsoft.com/en-us/ef/core/cli/dbcontext-creation)):

1. **`IDesignTimeDbContextFactory<TContext>`** — if found in the target or startup project, it bypasses everything else. Recommended for separate migrations projects and platform-specific startups.
2. Application services — the tools invoke the host builder of the startup project and pull the context from DI.
3. Parameterless constructor.

The tools default to the `Development` environment when no environment variable is set, and `dotnet ef migrations add` works by diffing the current model against the checked-in model snapshot — applying (not adding) is what needs a reachable database ([applying migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying), [migrations overview](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)). This matters under Aspire, where the real connection string only exists at run time inside orchestration: a per-slice design-time factory with a dummy/local connection string keeps `dotnet ef migrations add` working without booting anything (factory shape adapted from the [projects doc](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/projects)):

```csharp
public sealed class FindingsDbContextFactory : IDesignTimeDbContextFactory<FindingsDbContext>
{
    public FindingsDbContext CreateDbContext(string[] args)
        => new(new DbContextOptionsBuilder<FindingsDbContext>()
            .UseNpgsql(args.FirstOrDefault() ?? "Host=localhost;Database=podkop;Username=postgres",
                o => o.MigrationsAssembly(typeof(FindingsDbContextFactory).Assembly.GetName().Name))
            .Options);
}
```

Commands then target the Infrastructure project as both target and startup, which "prevents the tools from executing application startup code" ([projects doc](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/projects)):

```bash
dotnet ef migrations add InitialCreate \
    --project Features/Findings/Podkop.Findings.Infrastructure \
    --startup-project Features/Findings/Podkop.Findings.Infrastructure
```

(EF Core 11 will let repeated `--project`/`--startup-project` options live in `.config/dotnet-ef.json` — documented as "Starting with EF Core 11" in the [projects doc](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/projects); EF 11 is preview as of today.)

### Runtime pattern A — dedicated migration-service worker (the established tutorial pattern)

The [Aspire EF Core migrations doc](https://aspire.dev/integrations/databases/efcore/migrations/) builds a **separate Worker Service project** that references the data project(s) and ServiceDefaults. Its worker:

1. resolves the DbContext,
2. applies pending migrations with **`Database.MigrateAsync()`** (not `EnsureCreated` — the tutorial removes `EnsureCreated` from the API project), wrapped in `CreateExecutionStrategy()` for transient-fault handling,
3. seeds initial data,
4. calls `IHostApplicationLifetime.StopApplication()` so the worker exits.

Worker sketch (condensed from the doc's `Worker.ExecuteAsync`, PostgreSQL substituted; the doc's version also wraps seeding in an explicit transaction):

```csharp
protected override async Task ExecuteAsync(CancellationToken ct)
{
    using var scope = serviceProvider.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<FindingsDbContext>();

    var strategy = db.Database.CreateExecutionStrategy();
    await strategy.ExecuteAsync(async () => await db.Database.MigrateAsync(ct));
    await SeedDataAsync(db, ct);              // see section 4

    hostApplicationLifetime.StopApplication();
}
```

The AppHost gates the API on it (adapted from the same doc, PostgreSQL substituted):

```csharp
var postgres = builder.AddPostgres("postgres").WithDataVolume();
var podkopDb = postgres.AddDatabase("podkopdb");

var migrations = builder.AddProject<Projects.Podkop_MigrationService>("migrations")
    .WithReference(podkopDb)
    .WaitFor(podkopDb);

builder.AddProject<Projects.Podkop_Server>("api")
    .WithReference(podkopDb)
    .WaitForCompletion(migrations);   // API does not start until migrations finish
```

For multiple *databases* the doc recommends a dedicated migration service per database, and documents a single service migrating several DbContexts as the alternative — with one database and several contexts, one worker that migrates each context in sequence is the documented shape (same doc).

Production caveats come from the EF side, not Aspire: runtime migration is listed as acceptable "for applications that accept startup migration tradeoffs", but a separate deployment step (bundle or reviewed SQL script) is preferred when review, least-privilege credentials, coordinated rollout, or high availability matter. Since EF Core 9, `Migrate`/`MigrateAsync` take a database-wide **migration lock** (concurrent instances can no longer corrupt each other) and **throw on pending model changes** (`PendingModelChangesWarning`). Never call `EnsureCreated` before `Migrate` — it bypasses migrations and breaks them. ([Applying migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying))

### Runtime pattern B — `AddEFMigrations` (new, package still prerelease)

Aspire also ships an AppHost-level integration: `api.AddEFMigrations("api-migrations").RunDatabaseUpdateOnStart().PublishAsMigrationBundle(publishContainer: true)` — no separate worker project. In dev it drives migrations from the AppHost and adds dashboard commands (Update/Drop/Reset Database, Add/Remove Migration, Get Database Status, with automatic `dotnet-ef` tool handling); at publish time it emits a migration **bundle** (optionally a container image) or a SQL script for one-shot deployment jobs ([Aspire migrations doc](https://aspire.dev/integrations/databases/efcore/migrations/), [NuGet](https://www.nuget.org/packages/Aspire.Hosting.EntityFrameworkCore)). The EF docs cross-reference it as the way Aspire apps coordinate local execution and publish bundles/scripts ([applying migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying#containers-and-deployment-jobs)). Caveat: the docs page does not label it preview, but the `Aspire.Hosting.EntityFrameworkCore` package's only published versions are prerelease (13.4.6-preview.1, 2026-06-19) — so as of today this is a preview integration in practice.

## 3. Dev/test strategy

### What the EF Core testing docs actually say

The [testing overview](https://learn.microsoft.com/en-us/ef/core/testing/) is unusually opinionated:

- The **InMemory provider** "is highly limited and we discourage its use" — it cannot test transactions or raw SQL and diverges from relational behavior (e.g. case sensitivity). Mocking `DbSet` is discouraged for the same reasons.
- **SQLite in-memory** is a better fake but still diverges (no provider-specific functions, different SQL dialect).
- Testing **against the production database system** is recommended more often than developers assume: "we suggest giving this approach a chance."

The companion page [Testing against your production database system](https://learn.microsoft.com/en-us/ef/core/testing/testing-with-the-database) supplies the mechanics: Docker images for the database with "libraries like Testcontainers" managing them; an xUnit **class fixture** that creates and seeds the database once (`EnsureDeleted` + `EnsureCreated`); **transaction-with-implicit-rollback** to isolate write tests; collection fixtures + a `Cleanup()` per test for tests that themselves commit transactions; and — for efficient cleanup — the **[Respawn](https://github.com/jbogard/respawn)** package, which resets state by deleting rows in an order computed from foreign-key relationships (PostgreSQL is supported; usage is `Respawner.CreateAsync(...)` then `ResetAsync(...)`).

### Option 1 — Testcontainers + WebApplicationFactory (evolution of Podkop's current tests)

[Testcontainers.PostgreSql](https://dotnet.testcontainers.org/modules/postgres/) provides `PostgreSqlBuilder`/`PostgreSqlContainer` with `GetConnectionString()`; containers implement `IAsyncLifetime`, so an xUnit fixture starts/disposes them automatically. The [ASP.NET Core example](https://dotnet.testcontainers.org/examples/aspnet/) shows the combination pattern: subclass `WebApplicationFactory<TEntryPoint>`, start the container, and override the app's connection string in `ConfigureWebHost` via `builder.UseSetting(...)`. Sketch (adapted from those two pages plus the EF fixture guidance):

```csharp
public sealed class PostgresApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder().Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
        => builder.UseSetting("ConnectionStrings:podkopdb", _db.GetConnectionString());

    public Task InitializeAsync() => _db.StartAsync();          // + migrate/seed here
    public new Task DisposeAsync() => _db.DisposeAsync().AsTask();
}
```

Properties: in-process host (fast per-request), real PostgreSQL (full fidelity for SQL, transactions, schemas), one container per fixture/assembly amortizes the ~seconds of container startup; requires Docker on dev machines and CI. This slots directly into the existing per-slice `Podkop.<Feature>.Tests` WebApplicationFactory tests.

### Option 2 — Aspire.Hosting.Testing (full-orchestration tests)

[`Aspire.Hosting.Testing`](https://aspire.dev/testing/write-your-first-test/) runs the *whole AppHost*: `DistributedApplicationTestingBuilder.CreateAsync<Projects.Podkop_AppHost>()`, then `BuildAsync()`/`StartAsync()`, `app.CreateHttpClient("api")`, and `app.ResourceNotifications.WaitForResourceHealthyAsync("api")` before asserting. The harness disables the dashboard and randomizes proxied ports so test runs can execute concurrently. This exercises the real orchestration graph — Postgres container, migration service gating, connection-string flow — i.e. things WebApplicationFactory cannot see. Cost: every test run boots the full distributed app (containers included), so it is markedly slower and coarser-grained; the tutorial frames it for validating cross-service interaction rather than replacing single-app testing.

### Honorable mentions

- **Respawn** — reset between tests without dropping the database; pairs with either option ([repo](https://github.com/jbogard/respawn), recommended in the [EF testing docs](https://learn.microsoft.com/en-us/ef/core/testing/testing-with-the-database#efficient-database-cleanup)).
- **EF InMemory / SQLite** — fastest, zero infrastructure, but Microsoft discourages InMemory outright and warns SQLite diverges from PostgreSQL behavior ([testing overview](https://learn.microsoft.com/en-us/ef/core/testing/)); with schemas in play SQLite is a non-starter since it does not support schemas at all ([entity types doc](https://learn.microsoft.com/en-us/ef/core/modeling/entity-types#table-schema)).

Inference — cost/speed/fidelity summary: Testcontainers+WAF is the workhorse (high fidelity, moderate startup cost, per-slice scope); Aspire.Hosting.Testing is for a handful of solution-level smoke tests; in-memory doubles buy speed you will pay back in false confidence about relational behavior.

## 4. Seeding patterns for dev/demo data

The [EF data seeding doc](https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding) distinguishes four mechanisms:

1. **`UseSeeding` / `UseAsyncSeeding`** (EF 9+) — delegates on `DbContextOptionsBuilder`, called during `EnsureCreated`, `Migrate`/`MigrateAsync`, `dotnet ef database update`, and migration bundles — *even when no migrations were applied* — and protected by the migration lock. The doc's Tip labels this "the recommended way of seeding the database with initial data." Tooling and bundles invoke only the **synchronous** delegate, so `UseSeeding` must always be implemented even if the app uses async. SQL-script deployment never invokes them.
2. **Custom initialization logic** — seed before the app's main logic runs. The doc warns seeding "should not be part of the normal app execution" (concurrency between instances, and it forces the app identity to hold schema/write permissions); a dedicated initialization process is the sanctioned form — which is precisely the role the Aspire migration service's `SeedDataAsync` step plays ([Aspire migrations doc](https://aspire.dev/integrations/databases/efcore/migrations/)).
3. **`HasData`** — now officially renamed **"model managed data"** because "data seeding" set wrong expectations. Data is part of the model snapshot; migrations diff it into `InsertData`/`UpdateData`/`DeleteData`. Restrictions: primary keys must be hard-coded, no DB-generated keys, large data bloats snapshots, nothing non-deterministic. The doc scopes it to "static data that's not expected to change outside of migrations" (ZIP codes are its example) and explicitly redirects testing/temporary data to `UseSeeding`.
4. **Manual migration customization** — hand-written `migrationBuilder.InsertData(...)` for fixed values.

**Which fits environment-conditional demo data?** `HasData` is disqualified by design: dev/demo rows would become part of the schema's migration history and ship to production. That leaves two viable homes, both compatible with data that is "not part of the schema":

- **Seeding step in the migration service**, gated on `IHostEnvironment.IsDevelopment()` (or an explicit flag). Fits Podkop best (Inference): the existing static sample generators in each slice's Infrastructure (plus the `Podkop.Shared.Infrastructure` vocabulary) can be invoked there per-context, the app process never needs seeding code or elevated rights, and production publishes simply don't run the dev branch.
- **`UseSeeding`/`UseAsyncSeeding` registered only in Development.** Caution (Inference from the doc's invocation rules): these delegates run on *every* `Migrate`, including production bundles, so demo seeding must be conditional at registration time — registering it unconditionally would replay demo data wherever migrations run.

Idempotence is required either way — the doc's own examples check for existing rows before inserting. Environment-gated shape inside the migration service (Inference; composed from the seeding doc's idempotence idiom and the Aspire worker's `SeedDataAsync` step):

```csharp
private async Task SeedDataAsync(FindingsDbContext db, CancellationToken ct)
{
    if (!hostEnvironment.IsDevelopment())
    {
        return;                                   // demo data never reaches production
    }

    if (!await db.Findings.AnyAsync(ct))
    {
        db.Findings.AddRange(FindingsSampleData.Generate());   // existing static generators
        await db.SaveChangesAsync(ct);
    }
}
```

## 5. What the Aspire PostgreSQL integration provides out of the box

Version context: Aspire 13.4 is current (2026-06-01); Aspire versions independently of .NET, ships roughly one major per year, and only the latest release is supported ([support policy](https://aspire.dev/support/)). The docs moved from learn.microsoft.com to aspire.dev (observed via 301 redirects). Current package versions on NuGet: `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` **13.4.6** (2026-06-19); the hosting package is `Aspire.Hosting.PostgreSQL` (installable via `aspire add postgres`).

### Hosting integration (`Aspire.Hosting.PostgreSQL`) — [postgres-host doc](https://aspire.dev/integrations/databases/postgres/postgres-host/)

- `AddPostgres("postgres")` runs the official `postgres` container — default image **PostgreSQL 18.3 since Aspire 13.4** (previously 17.6). Warning from the doc: PostgreSQL 18 changed the data-directory layout, so data volumes created under Aspire ≤13.3 (PG 17) are incompatible; pin `WithImageTag("17.6")` or migrate.
- `AddDatabase("podkopdb")` models the database **and actually creates it** once the server is ready (via `ResourceReadyEvent` and a default creation script) — the app does not need `EnsureCreated` for database existence.
- Credentials: username `postgres` + auto-generated password parameter by default; override with `AddParameter(..., secret: true)`.
- Persistence: `WithDataVolume()` (named volume, recommended) or `WithDataBindMount()`; `WithLifetime(ContainerLifetime.Persistent)` keeps the container running between app runs. Without these, dev data is ephemeral ([Aspire migrations doc](https://aspire.dev/integrations/databases/efcore/migrations/) notes rows don't survive an app restart).
- Tooling: `WithPgAdmin()` and `WithPgWeb()` add admin UIs; `WithInitBindMount()` mounts first-startup init scripts.
- Health: the hosting integration registers server-connectivity health checks (via `AspNetCore.HealthChecks.Npgsql`), which `WaitFor` uses to gate dependents.
- `WithReference(podkopDb)` injects the connection string into a consuming project as `ConnectionStrings__podkopdb` ([WithReference API doc](https://learn.microsoft.com/en-us/dotnet/api/aspire.hosting.resourcebuilderextensions.withreference)); Aspire 13's polyglot support also injects `[RESOURCE]_[PROPERTY]`-style variables for non-.NET consumers ([postgres get-started](https://aspire.dev/integrations/databases/postgres/postgres-get-started/)).

### Client integration (`Aspire.Npgsql.EntityFrameworkCore.PostgreSQL`) — [postgresql-connect doc](https://aspire.dev/integrations/databases/efcore/postgres/postgresql-connect/)

Two registration styles:

- **`builder.AddNpgsqlDbContext<TContext>("podkopdb")`** — registers the context and wires connection string, retries, health check, logging, and telemetry in one call.
- **`builder.EnrichNpgsqlDbContext<TContext>(...)`** — for when you register the DbContext yourself with `AddDbContext`/`UseNpgsql` first (the caveat is explicit: register first, then enrich) and want Aspire to layer on retries, health checks, logging, telemetry. This is the route when the `UseNpgsql` options need to carry `MigrationsAssembly`/`MigrationsHistoryTable` configuration (Inference from the two APIs' division of labor).

What it configures: retry-on-failure **enabled by default** (`DisableRetry` to opt out; note the EF caveat that user-initiated transactions require execution-strategy wrapping — [EF transactions doc](https://learn.microsoft.com/en-us/ef/core/saving/transactions)); a `DbContextHealthCheck` per context calling `CanConnectAsync`, feeding the standard `/health` endpoint; OpenTelemetry tracing for Npgsql plus EF Core and Npgsql metrics (query counts, connection usage, cache hit rate, etc.); `CommandTimeout`. Configuration lives under `Aspire:Npgsql:EntityFrameworkCore:PostgreSQL`, with **per-context named sub-sections** for multiple DbContexts; multiple contexts are registered by repeating the call per context type. NuGet dependency floor on net10.0 is `Npgsql.EntityFrameworkCore.PostgreSQL >= 10.0.0` — i.e. full EF Core 10 / .NET 10 support in the current line ([NuGet](https://www.nuget.org/packages/Aspire.Npgsql.EntityFrameworkCore.PostgreSQL)).

A plain-Npgsql client integration (`Aspire.Npgsql`, registering `NpgsqlDataSource` without EF) also exists ([postgres get-started](https://aspire.dev/integrations/databases/postgres/postgres-get-started/)).

## Shape recommendation for Podkop

Framing: this is **input for a follow-up decision session, not a decision**. Options are listed where more than one arrangement is defensible; leans are marked as such.

1. **One PostgreSQL server, one database, one schema per slice** (`findings`, `findingcomments`, `documents`, `users`, `moderation`, later `tags` — exact schema names are a decision-session item) — Milan's recommended starting level, and the only option that doesn't fight the existing project structure. Alternatives: a single shared DbContext (simpler migrations, but it needs a shared persistence project every slice references — dissolving slice isolation at the data layer) or database-per-slice (strict isolation, but N databases to run and no cross-context transactions; overkill for a prototype per [Milan](https://www.milanjovanovic.tech/blog/modular-monolith-data-isolation)). **Lean: schema-per-slice.**
2. **One DbContext per slice, living in `Podkop.<Feature>.Infrastructure`** alongside its migrations and an `IDesignTimeDbContextFactory`. Configure each with `HasDefaultSchema("<slice>")`, `MigrationsHistoryTable("__EFMigrationsHistory", "<slice>")`, and `MigrationsAssembly(...)` (section 1 snippet). `dotnet ef` commands target the Infrastructure project as both `--project` and `--startup-project`. No extra per-slice Migrations project — Infrastructure already is the separate project the EF docs' layout wants (Inference).
3. **Decision point — cross-slice references:** slices reference other slices' aggregates today only by ID via Contracts. With one database, cross-schema foreign keys are *physically* possible but violate the module rules Milan states (no touching other modules' tables). Options: (a) no cross-schema FK constraints — IDs are plain columns, integrity maintained at the application/contract level (consistent with the architecture, weaker DB integrity); (b) allow FK constraints across schemas as a pragmatic monolith concession (stronger integrity, harder future extraction, and the owning migration stream becomes ambiguous). **Lean: (a)**, matching the existing Contracts-only rule; this deserves explicit human sign-off.
4. **Migrations runner: a dedicated `Podkop.MigrationService` worker project** (pattern A, section 2) referencing every slice's Infrastructure project, applying `MigrateAsync` per context sequentially (one database → single worker is the documented shape), then running the environment-gated seeding step, then `StopApplication`. AppHost: worker `WithReference(podkopDb).WaitFor(podkopDb)`; `Podkop.Server` `WaitForCompletion(migrations)`. Note the worker is the one place that legitimately references multiple slices' Infrastructure — an explicit, reviewable exception to the isolation rule, like `Podkop.Server` is for Server projects (Inference). Revisit `AddEFMigrations` (pattern B) once `Aspire.Hosting.EntityFrameworkCore` leaves prerelease — it would remove the worker project and add dashboard commands, and its publish story (bundle/one-shot job) is the EF-recommended production mechanism anyway.
5. **Testing: keep WebApplicationFactory, add Testcontainers.PostgreSql** — a shared fixture per test project (or collection) that starts one PostgreSQL container, runs the slice's migrations, and overrides `ConnectionStrings:podkopdb`; Respawn (or transaction rollback for simple cases) between tests. This preserves the current per-slice endpoint-test idiom and follows the EF docs' explicit recommendation to test against the real database system. Optionally add a *small* `Aspire.Hosting.Testing` smoke suite at solution level to cover orchestration (migration gating, connection flow). Do not adopt InMemory/SQLite — discouraged by Microsoft, and SQLite cannot represent the schema-per-slice layout at all.
6. **Seeding: migrate the existing static sample generators into the migration service's seed step**, gated to Development. They already live per-slice in Infrastructure (with shared vocabulary in `Podkop.Shared.Infrastructure`), so each slice keeps owning its demo data; the worker just invokes them idempotently. No `HasData` for demo data (model-managed data is for static reference data only); `UseSeeding` remains available later for genuine invariant reference data if any appears.
7. **AppHost wiring:** `AddPostgres("postgres").WithDataVolume().WithLifetime(ContainerLifetime.Persistent)` + `AddDatabase("podkopdb")`; `WithPgAdmin()` optional for dev. Per-slice registration via each slice's existing DI entry point using `AddDbContext` + `EnrichNpgsqlDbContext` (Enrich rather than `AddNpgsqlDbContext`, because the `UseNpgsql` call must carry per-slice migrations options — section 5). Be aware of the PG 17→18 volume-compatibility note when upgrading Aspire with an existing data volume.

Open questions to settle in the decision session: cross-slice FK policy (point 3); whether Moderation's case data needs transactional coupling with Findings state changes (if yes, the shared-connection `UseTransaction` mechanism from section 1 works within one database — another argument against database-per-slice); when to jump from the worker to `AddEFMigrations`; and whether a Testcontainers-based fixture should live in a shared test utility project or be duplicated per slice (slices' tests are currently fully independent).
