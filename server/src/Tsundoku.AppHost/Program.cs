// Local development: Postgres and Valkey in persistent containers, the API, and the Aspire dashboard.
// The Postgres password is in the user secrets of this project, not in the repository:
//   dotnet user-secrets set Parameters:postgres-password <password> --project src/Tsundoku.AppHost
//   dotnet user-secrets set Parameters:app-db-password <password> --project src/Tsundoku.AppHost
// The first is the owner (migrations); the second is the password of the tsundoku_api login the API runs as.

var builder = DistributedApplication.CreateBuilder(args);

var password = builder.AddParameter("postgres-password", secret: true);
var appDbPassword = builder.AddParameter("app-db-password", secret: true);

// Keep the tag the same as the test container in tests/Tsundoku.Api.Tests/Integration/ApiFactory.cs.
var postgres = builder.AddPostgres("postgres", password: password)
    .WithImageTag("18")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

// The name must be the same as ModulePersistence.ConnectionStringName in Tsundoku.Common.
var database = postgres.AddDatabase("appdb");

// Keep the tag the same as the test container in tests/Tsundoku.Api.Tests/Integration/ApiFactory.cs.
// The name must be the same as ValkeyRegistration.ConnectionStringName in Tsundoku.Common.
var valkey = builder.AddValkey("valkey")
    .WithImageTag("8")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

// The path form, not Projects.Tsundoku_Api: the generated type name changes when `dotnet new -n` adds dots to the name.
var api = builder.AddProject("api", "../Tsundoku.Api/Tsundoku.Api.csproj");

// The names must be the same as ModulePersistence.OwnerConnectionStringName and ConnectionStringName in Tsundoku.Common.
// The owner migrates; the API itself runs as tsundoku_api, which is neither the owner nor BYPASSRLS.
var endpoint = postgres.GetEndpoint("tcp");
api.WithEnvironment("ConnectionStrings__appdb-owner", database)
    .WithEnvironment("ConnectionStrings__appdb", ReferenceExpression.Create(
        $"Host={endpoint.Property(EndpointProperty.Host)};Port={endpoint.Property(EndpointProperty.Port)};Database=appdb;Username=tsundoku_api;Password={appDbPassword}"))
    .WithReference(valkey)
    .WithEnvironment("Accounts__SignUpEnabled", "true") // Development only: the log mailer writes the confirmation link
    .WithEnvironment("Accounts__PublicBaseUrl", api.GetEndpoint("http"))
    .WaitFor(database)
    .WaitFor(valkey);

await builder.Build().RunAsync();
