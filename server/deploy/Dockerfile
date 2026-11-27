# The API as a container. Build context is the template root:
#   docker build -f deploy/Dockerfile -t tsundoku-api .
# Migrations run as their own deploy step before the new API starts (tip 28.01), not at API startup (the compose file
# is deploy/compose.yaml at the repository root):
# the same image with `migrate` as its argument (dotnet Tsundoku.Api.dll migrate).

FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src
# Project files first, so the restore layer is reused until a dependency changes.
COPY global.json dotnet-tools.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/Tsundoku.Common/Tsundoku.Common.csproj src/Tsundoku.Common/
COPY src/Modules/Tsundoku.Modules.Accounts/Tsundoku.Modules.Accounts.csproj src/Modules/Tsundoku.Modules.Accounts/
COPY src/Modules/Tsundoku.Modules.Todos/Tsundoku.Modules.Todos.csproj src/Modules/Tsundoku.Modules.Todos/
COPY src/Tsundoku.ServiceDefaults/Tsundoku.ServiceDefaults.csproj src/Tsundoku.ServiceDefaults/
COPY src/Tsundoku.Api/Tsundoku.Api.csproj src/Tsundoku.Api/
RUN dotnet restore src/Tsundoku.Api/Tsundoku.Api.csproj
COPY src/ src/
RUN dotnet publish src/Tsundoku.Api/Tsundoku.Api.csproj -c Release -o /app --no-restore

# Chiseled: no shell, no package manager, and a non-root user by default (tip 27.01).
# The -extra image carries the IANA time zone data that TimeZoneId and the engine need (plain chiseled has none).
# InvariantGlobalization stays true: IANA lookups need tzdata, not ICU.
# It has no curl either, so the orchestrator probes /health/live and /health/ready over HTTP.
FROM mcr.microsoft.com/dotnet/aspnet:11.0-resolute-chiseled-extra AS api
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
# Exec form: SIGTERM goes to dotnet, so the graceful shutdown runs (tip 18.09).
ENTRYPOINT ["dotnet", "Tsundoku.Api.dll"]
