# The API as a container. Build context is the template root:
#   docker build -f deploy/Dockerfile -t tsundoku-api .                    # the API (default target)
#   docker build -f deploy/Dockerfile --target migrate -t tsundoku-migrate .  # the EF migration bundle
# Migrations run as their own deploy step before the new API starts (tip 28.01), not at API startup.

FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src
# Project files first, so the restore layer is reused until a dependency changes.
COPY global.json dotnet-tools.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/Tsundoku.Common/Tsundoku.Common.csproj src/Tsundoku.Common/
COPY src/Modules/Tsundoku.Modules.Todos/Tsundoku.Modules.Todos.csproj src/Modules/Tsundoku.Modules.Todos/
COPY src/Tsundoku.ServiceDefaults/Tsundoku.ServiceDefaults.csproj src/Tsundoku.ServiceDefaults/
COPY src/Tsundoku.Api/Tsundoku.Api.csproj src/Tsundoku.Api/
RUN dotnet restore src/Tsundoku.Api/Tsundoku.Api.csproj
COPY src/ src/
RUN dotnet publish src/Tsundoku.Api/Tsundoku.Api.csproj -c Release -o /app --no-restore

FROM build AS bundle
# One bundle for each module that has migrations (today only Todos).
RUN dotnet tool restore \
    && dotnet ef migrations bundle --project src/Modules/Tsundoku.Modules.Todos --startup-project src/Modules/Tsundoku.Modules.Todos \
       --configuration Release --output /efbundle

# The migration bundle: run it with --connection "<connection string>".
# It needs the ASP.NET Core framework, because Common uses it (health checks, hosted services).
FROM mcr.microsoft.com/dotnet/aspnet:11.0-resolute-chiseled AS migrate
COPY --from=bundle /efbundle /efbundle
ENTRYPOINT ["/efbundle"]

# Chiseled: no shell, no package manager, and a non-root user by default (tip 27.01).
# It has no ICU and no time zone data: the API sets InvariantGlobalization. When you need
# time zones or cultures, use the 11.0-resolute-chiseled-extra image instead (tip 27.02).
# It has no curl either, so the orchestrator probes /health/live and /health/ready over HTTP.
FROM mcr.microsoft.com/dotnet/aspnet:11.0-resolute-chiseled AS api
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
# Exec form: SIGTERM goes to dotnet, so the graceful shutdown runs (tip 18.09).
ENTRYPOINT ["dotnet", "Tsundoku.Api.dll"]
