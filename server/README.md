# Tsundoku: a vertical-slice modular monolith

An ASP.NET Core API on .NET 11 / C# 15 with PostgreSQL. One host, one shared project, and one project for each module.
A feature is one folder inside its module, not a file in each of four layer projects.

- aggregates, value objects and domain events stay rich where the invariants are
- handlers use the module's `DbContext` directly: no repositories
- a transactional outbox for each module

## Run it

```bash
# 1. Development: Postgres in a container, the API, and the Aspire dashboard.
dotnet user-secrets set Parameters:postgres-password <password> --project src/Tsundoku.AppHost
dotnet run --project src/Tsundoku.AppHost


# 2. Tests (Docker must run for the integration tests).
dotnet test --solution Tsundoku.slnx
```

**Note:** `dotnet run --environment X` sets an environment variable in .NET 11. To choose the ASP.NET Core environment, set `ASPNETCORE_ENVIRONMENT`.

Production-like, in containers: `cp deploy/.env.example deploy/.env`, fill it in, then `docker compose -f deploy/compose.yaml --env-file deploy/.env up --build`.

## Layout

```text
src/
  Tsundoku.Common/            Building blocks that every module shares. Public API: keep it small.
    Domain/                  Entity, AggregateRoot, IDomainEvent, DomainException, UserId, Results/ (Result<T>, closed Error)
    Messaging/               ICommand/IQuery + handlers, Logging → Validation → UnitOfWork decorators, AppMetrics, PagedResponse
    Events/                  Domain event registry, dispatcher, IDomainEventHandler
    Persistence/             Snake case names, converters, audit interceptor, ConcurrencyException,
                             outbox (generic over the module context), health check, migration helper
    Identity/                ICurrentUser
    Http/                    ResultHttp: Result → problem details
    Valkey/                  One multiplexer and its health check
  Modules/
    Tsundoku.Modules.Todos/  The sample module (the goals module replaces it)
      TodosModule.cs         The ONLY public type: AddTodosModule, MapTodosEndpoints, MigrateTodosAsync
      Domain/                TodoList, TodoItem, Title, ids, events, errors
      Features/<UseCase>/    Command or Query, validator, handler, endpoint, request and response records
      Persistence/           TodosDbContext (schema "todos"), mappings, Migrations/
  Tsundoku.Api/               The host only: Program, Hosting/ (auth, limits, proxies, module list), exception handler, HttpCurrentUser
  Tsundoku.Engine/            The pure fold (no packages, no I/O, no clock)
  Tsundoku.ServiceDefaults/   OpenTelemetry, health endpoints, HttpClient resilience
  Tsundoku.AppHost/           Aspire: Postgres + Valkey + API for development
tests/
  Tsundoku.Modules.Todos.Tests/  Aggregate and value object rules
  Tsundoku.Engine.Tests/         The fold, against the fixtures
  Tsundoku.Architecture.Tests/   The rules below, checked in IL and in source files
  Tsundoku.Api.Tests/            HTTP end to end, outbox and concurrency, with Testcontainers Postgres
deploy/                      Dockerfile (API + migration bundle), compose
```

## The rules of this architecture

- **Common knows no module and no host.** A module references Common and never another module. When one needs another, add a `Tsundoku.Modules.X.Contracts` project with public integration events or a query interface then.
- **A module has one public type**, `<Name>Module`. Everything else is `internal`, so the compiler enforces the boundary.
- **A module owns its data:** one `DbContext`, one schema, its own migration history and its own `outbox_messages` table in that schema.
- **The module domain is plain C#** on top of Common: it uses no EF Core, ASP.NET Core or FluentValidation, and not the module's `Features` or `Persistence`.
- **No repositories.** Handlers inject the module context. A command loads the aggregate with the owner in the lookup (`SingleOrDefaultAsync(l => l.Id == id && l.OwnerId == owner)`), so another user's list is a 404 (tip 19.05). A query projects to DTOs with `AsNoTracking`. A query moves to its own class only when two slices use it.
- **Handlers never save.** The unit-of-work decorator saves one time, only after a success, and maps `DbUpdateConcurrencyException` to `ConcurrencyException` (409). After a refusal nothing is saved. The tests fail when a handler calls `SaveChanges`.
- **Commands return only an id or `Success`.** Queries never load an aggregate to change it.
- **Value objects are always valid.** `Title.Create` is the only way to make a `Title`. `UserId` refuses an empty id.
- **Every refusal is a `Result<T>`** with an `Error` from the `closed` hierarchy. `ResultHttp.ToProblem` switches over all kinds with no default arm, so a new kind of error is a compile error until it has an HTTP status.
- **No public setters in the domain.** State changes through methods (tip 25.05). Each change raises the aggregate `Version`, the concurrency token.
- **Nothing is shared between use cases** unless two use it. Then it goes to the `Features/` root (`TitleRules`, `IdResponse`).
- **The engine is pure.** No I/O, no clock, no randomness, no packages.
- **Logging uses `[LoggerMessage]` only.** No MediatR, AutoMapper or Scrutor. Registration is explicit, with the decorator chain written with `new`.

## The outbox

1. An aggregate raises a domain event during a change.
2. `OutboxInterceptor` writes the event as an `outbox_messages` row (in the module's own schema) **in the same `SaveChangesAsync`**. The change and the event commit together, or neither does (tip 17.04).
3. `OutboxProcessor<TDbContext>` (a `BackgroundService`, one for each module) claims a batch with `FOR UPDATE SKIP LOCKED`, so several replicas can run it (tip 17.05). It calls the handlers through `DomainEventDispatcher` and marks each row as processed.
4. A failed message is tried again. After `Outbox:MaxAttempts` it stays as a dead letter, with `last_error` (tip 17.07).

**Warning:** Delivery is at least once. A crash after a handler ran but before the commit runs that handler again. Make each handler idempotent (tip 17.06).

Event types are stored by their class name, from `DomainEventRegistry`, which is shared by all modules, so an event name must be unique in the whole app. Register each event type: use `AddDomainEventHandler` when it has a handler, or `AddDomainEvent` when it has none yet.
An event payload must stay readable by the next version of the app, so add properties, but do not rename or remove them.

## Add a use case

1. Domain: add the behavior as a method of the aggregate that returns `Result<T>`. Add new refusals to `<Aggregate>Errors`. Add domain tests.
2. Make a folder `Features/<UseCase>/` in the module with:
   - `<UseCase>Command` or `<UseCase>Query`, its validator, and its request and response records
   - `<UseCase>Handler`, `internal sealed`, that uses the module context
   - `<UseCase>Endpoint`: use `ToOk`, `ToCreated` or `ToNoContent`
3. Add the registration lines in `<Name>Module.AddTodosModule`: `AddCommand<…, TodosDbContext>()` or `AddQuery<…>()`, plus `AddValidator<…>()`, and the endpoint line in `MapTodosEndpoints`.
4. When the model changes, from `server/`:
   `dotnet ef migrations add <Name> --project src/Modules/Tsundoku.Modules.Todos --startup-project src/Modules/Tsundoku.Modules.Todos --output-dir Persistence/Migrations`

## Add a module

1. Make `src/Modules/Tsundoku.Modules.<Name>` (copy the csproj of Todos), with `<Name>Module` as its only public type, a `DbContext` that calls `ModulePersistence.ConfigureModel` and `ConfigureConventions`, and its own schema.
2. In `<Name>Module.Add<Name>Module` call `builder.AddModuleDatabase<TContext>("<schema>")`, then register its use cases.
3. Add one line each to `AddModules`, `MapModules` and `MigrateModulesAsync` in `Tsundoku.Api/Hosting/ApiModules.cs`, the project to `Tsundoku.slnx`, and the assembly to `Modules` in the architecture tests.
4. Add its project and a migration bundle step to `deploy/Dockerfile`, and make its first migration with the command above.

## Before production

- [ ] Set `ApiLimits:KnownProxies` to the address of your reverse proxy.
- [ ] Set `ApiLimits:RequestsPerMinute`. The limit is for each instance (tip 15.10).
- [ ] Run the migration bundle (`--target migrate`) as a deploy step, with expand/contract changes (tip 28.01).
- [ ] Monitor the outbox: alert, in the outbox table of each module, on rows where `processed_at IS NULL AND attempts >= MaxAttempts` (dead letters), and on old unprocessed rows.
- [ ] Delete processed outbox rows after some days, with a scheduled job.
- [ ] Set `OTEL_EXPORTER_OTLP_ENDPOINT` and add alerts on the 5xx rate and `app.use_case.duration`.
- [ ] Probe `/health/live` (liveness) and `/health/ready` (readiness). Give the container a grace period of 35 s or more.
- [ ] Size the connection pool: (`max_connections` − reserve) ÷ instances (tip 10.27).
