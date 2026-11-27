# Tsundoku: a vertical-slice modular monolith

An ASP.NET Core API on .NET 11 / C# 15 with PostgreSQL. One host, one shared project, and one project for each module.
A feature is one folder inside its module, not a file in each of four layer projects.

- aggregates, value objects and domain events stay rich where the invariants are
- handlers use the module's `DbContext` directly: no repositories
- a transactional outbox for each module

## Run it

```bash
# 1. Development: Postgres and Valkey in containers, the API, and the Aspire dashboard.
#    The first password is the database owner (migrations), the second is the tsundoku_api login the API runs as.
dotnet user-secrets set Parameters:postgres-password <password> --project src/Tsundoku.AppHost
dotnet user-secrets set Parameters:app-db-password <password> --project src/Tsundoku.AppHost
dotnet run --project src/Tsundoku.AppHost


# 2. Tests (Docker must run for the integration tests).
dotnet test --solution Tsundoku.slnx
```

**Note:** `dotnet run --environment X` sets an environment variable in .NET 11. To choose the ASP.NET Core environment, set `ASPNETCORE_ENVIRONMENT`.

Production-like, in containers: `cp deploy/.env.example deploy/.env`, fill it in, then `docker compose -f deploy/compose.yaml --env-file deploy/.env up --build`.
It starts Postgres and Valkey, runs `migrate` (the API image with the argument `migrate`), then the API on 127.0.0.1:8080.

## Sign in

The API needs three connection strings: `appdb-owner` (the database owner: migrations, roles, grants), `appdb` (the `tsundoku_api` login: neither the owner nor BYPASSRLS, and readiness refuses an unsafe role) and `valkey`.

1. Create your account: sign-up is closed by default (`Accounts:SignUpEnabled`, env `SIGNUP_ENABLED`). Set it true once, call `POST /api/auth/sign-up`, then set it back. In Development it is on.
2. The mail is only logged for now: find the confirmation link in the API log and open it (outside Development, sign-up enabled with the log mailer fails startup).
3. Sign in with `src/Tsundoku.Api/Tsundoku.Api.http` (`POST /api/auth/sign-in`): the bearer access token goes in `Authorization`, and `/api/auth/refresh` gives new tokens (a refresh token works once).
4. Google: the client signs in with Google itself and posts the ID token to `POST /api/auth/google`. Set the accepted audiences in `Accounts:Google:ClientIds` (env `GOOGLE_CLIENT_ID`).

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
    Valkey/                  One multiplexer, its health check, the shared token-bucket rate limits
  Modules/
    Tsundoku.Modules.Accounts/ Identity users, bearer and refresh tokens, sessions, Google sign-in, user settings (schema "accounts")
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
deploy/                      Dockerfile (the API image, also the migrate step), compose
```

## The rules of this architecture

- **Common knows no module and no host.** A module references Common and never another module. When one needs another, add a `Tsundoku.Modules.X.Contracts` project with public integration events or a query interface then.
- **A module has one public type**, `<Name>Module`. Everything else is `internal`, so the compiler enforces the boundary.
- **A module owns its data:** one `DbContext`, one schema, its own migration history and its own `outbox_messages` table in that schema.
- **The module domain is plain C#** on top of Common: it uses no EF Core, ASP.NET Core or FluentValidation, and not the module's `Features` or `Persistence`.
- **Row-level security guards every owned table.** An owned table has a `user_id` column and `AddOwnerPolicy` in its migration. The API role is not the owner, so the policy binds it. A handler reaches the table only through the pipeline: the transaction decorator sets `app.user_id` with `set_config(..., true)` from the authenticated user, and Postgres clears it at commit. A query uses `AddQuery<…, TDbContext>` (it gets a transaction too). A use case with no signed-in user (sign-up, sign-in) uses `AddAnonymousCommand` and may touch only the RLS-exempt auth tables.
- **The API role is not the owner.** Migrations run as the owner (`appdb-owner`); the API runs as `tsundoku_api`, which gets only DML through `GrantAppRole`.
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
3. Add the registration lines in `<Name>Module.AddTodosModule`: `AddCommand<…, TodosDbContext>()`, `AddQuery<…, TodosDbContext>()` or `AddAnonymousCommand<…>()`, plus `AddValidator<…>()`, and the endpoint line in `MapTodosEndpoints`.
4. When the model changes, from `server/`:
   `dotnet ef migrations add <Name> --project src/Modules/Tsundoku.Modules.Todos --startup-project src/Modules/Tsundoku.Modules.Todos --output-dir Persistence/Migrations`

## Add a module

1. Make `src/Modules/Tsundoku.Modules.<Name>` (copy the csproj of Todos), with `<Name>Module` as its only public type, a `DbContext` that calls `ModulePersistence.ConfigureModel` and `ConfigureConventions`, and its own schema.
2. In `<Name>Module.Add<Name>Module` call `builder.AddModuleDatabase<TContext>("<schema>")`, then register its use cases.
3. Add one line each to `AddModules`, `MapModules` and `MigrateModulesAsync` in `Tsundoku.Api/Hosting/ApiModules.cs`, the project to `Tsundoku.slnx`, and the assembly to `Modules` in the architecture tests.
4. Add its project file to `deploy/Dockerfile` (the restore layer). There is no bundle: its first migration calls `GrantAppRole(schema)`, and `migrate` runs it with the others.

## Before production

- [ ] Set `ApiLimits:KnownProxies` to the address of your reverse proxy.
- [ ] Review `RateLimits:Credentials`, `RateLimits:User` and `RateLimits:Anonymous` (`TokenLimit` and `ReplenishmentPeriod`). `RateLimits:Ipv6PrefixLength` sets how many bits of an IPv6 address make one client.
- [ ] Run `dotnet Tsundoku.Api.dll migrate` (the compose `migrate` service) as a deploy step, with expand/contract changes (tip 28.01).
- [ ] Back up `accounts.data_protection_keys`: without the key ring every issued token and confirmation link stops working.
- [ ] Rate limits are shared in Valkey now, not per instance. Valkey is ephemeral here: a restart resets the buckets, and the limiter fails open.
- [ ] Monitor the outbox: alert, in the outbox table of each module, on rows where `processed_at IS NULL AND attempts >= MaxAttempts` (dead letters), and on old unprocessed rows.
- [ ] Delete processed outbox rows after some days, with a scheduled job.
- [ ] Set `OTEL_EXPORTER_OTLP_ENDPOINT` and add alerts on the 5xx rate and `app.use_case.duration`.
- [ ] Probe `/health/live` (liveness) and `/health/ready` (readiness). Give the container a grace period of 35 s or more.
- [ ] Size the connection pool: (`max_connections` − reserve) ÷ instances (tip 10.27).
