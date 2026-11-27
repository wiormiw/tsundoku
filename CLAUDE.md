# Tsundoku

A goal tracker (the improvement of rnd). The API is being ported to .NET 11 / C# 15 in `server/`
(ASP.NET Core, EF Core + Npgsql, PostgreSQL 18, Valkey 8, OpenTelemetry), started from the
user's `clean-ddd` template (`~/Personal/GitProj/dotnet-boilerplates/clean-ddd`), restructured as a
vertical-slice modular monolith (one project per module). Aspire AppHost in development, the compose LGTM stack (`deploy/`) in
production. The design lives in `docs/specs/` (local, gitignored; read it if present). The earlier
Bun/TypeScript backend (slices B1–B4 complete) is the reference implementation, kept outside this
repo at `~/Personal/GitProj/tsundoku-bun-archive` (its own git history); read it for behaviour, never
copy its files back.

## Commands

```
export PATH="$HOME/dotnet:$PATH"                     the SDK (11.0.100) lives there
dotnet build server/Tsundoku.slnx                     warnings are errors
cd server && dotnet test --solution Tsundoku.slnx      from server/ (its global.json picks the test runner); needs Docker
dotnet run --project server/src/Tsundoku.AppHost      Aspire: Postgres 18, Valkey 8, the API, the dashboard
```

`server/README.md` is the template's guide: layout, architecture rules, how to add a use case.

## Rules

- **Fixtures are the contract.** `fixtures/engine/*.json` (the fold) and `fixtures/sync/*.json`
  (the sync protocol) are language-neutral; the API's tests read them in place and must pass all
  of them. Change a fixture only on purpose, never to make a test pass.
- **The engine is pure.** No I/O, no clock (`today` is an input), no randomness, no packages; an
  architecture test enforces it.
- **Follow the template's architecture rules** (its README) and `~/Personal/GitProj/CSharp-Tips`;
  use the `csharp-15` and `dotnet-backend` skills.
- **High-performance code on hot paths** (the fold, JSON, per-request work): `Span<T>`/`ReadOnlySpan<T>`,
  `stackalloc` for small buffers, `ArrayPool<T>.Shared` for big temporary arrays (returned in `finally`),
  `CollectionsMarshal.AsSpan`, `Frozen*` collections for fixed lookups, cached `JsonSerializerOptions`.
  Measure with BenchmarkDotNet first (`CSharp-Tips` chapter 6); simple code where nothing is hot.
- **Define a thing once; derive the rest.** No duplicated rules, types or value sets.
- **Tunables are config, never a constant in code** (timeouts, TTLs, limits, sizes, rates): an
  options class validated at startup, with its default in one place.
- **Comments say why, briefly.**
- **`docs/`, `.superpowers/`, `bench/`, `*.plan.md` and `.env` stay out of git.** Never `git add -A`
  blindly or `git add -f` them; the README must stand alone and not link to docs/.
- **Commits carry no Co-Authored-By or attribution line.**
- **Docker: only the compose project `tsundoku` and Aspire's own containers.** Other projects run
  on this machine (akashic-prod, old-country-local, postgres-9aff4cf4); never stop, restart, exec
  into or `down` any other container.

Ports (127.0.0.1): Postgres 4632, Valkey 4679, OTLP/gRPC 4617, OTLP/HTTP 4618, Grafana 4630,
Prometheus 4690, Loki 4610, Tempo 4620.
