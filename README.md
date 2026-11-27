# Tsundoku

A goal tracker that fights procrastination: goals made of areas and milestones, a daily floor and
stretch per activity, OFFLINE days with a monthly allowance, debt for missed work that grows a
little with each missed day, and a retrospective at the end. Offline-first desktop and Android
clients sync with one API.

Status: the API is being built in .NET 11 / C# 15 (ASP.NET Core, PostgreSQL 18, Valkey 8,
OpenTelemetry), with Aspire for development and the Grafana LGTM stack in production.

## Layout

```
server/            the API: .NET 11 solution (vertical-slice modular monolith), Aspire AppHost, tests
fixtures/engine/   language-neutral tables: a fact log in, the expected goal state out
fixtures/sync/     sync scenarios: client state + server responses in, client state out
deploy/            compose: Postgres, Valkey, OpenTelemetry Collector, Tempo, Loki, Prometheus, Grafana
```

Every implementation (the API, the Rust and Kotlin clients) runs the same fixture files.

## Run the API (development)

```
dotnet user-secrets set Parameters:postgres-password <password> --project server/src/Tsundoku.AppHost
dotnet user-secrets set Parameters:app-db-password <password> --project server/src/Tsundoku.AppHost
dotnet run --project server/src/Tsundoku.AppHost      # Postgres, Valkey, the API and the Aspire dashboard
cd server && dotnet test --solution Tsundoku.slnx       # from server/; Docker must run for the integration tests
```

## Auth

Email and password (with email confirmation) and Google sign-in by ID token; the API issues bearer and single-use refresh tokens.
Sign-up is closed unless `SIGNUP_ENABLED=true`: open it once to create your account, then close it.
Every owned table is under row-level security, and the API connects as a role that is neither the owner nor BYPASSRLS.
See `server/README.md` for the flow and for production.

## Observability stack

```
docker compose -f deploy/compose.yaml up -d --wait
```

Grafana on 127.0.0.1:4630, OTLP/gRPC on 4617, OTLP/HTTP on 4618.
