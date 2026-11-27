// Local development: Postgres and Valkey in persistent containers, the API, and the Aspire dashboard.
// The Postgres password is in the user secrets of this project, not in the repository:
//   dotnet user-secrets set Parameters:postgres-password <password> --project src/Tsundoku.AppHost

var builder = DistributedApplication.CreateBuilder(args);

var password = builder.AddParameter("postgres-password", secret: true);

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
builder.AddProject("api", "../Tsundoku.Api/Tsundoku.Api.csproj")
    .WithReference(database)
    .WithReference(valkey)
    .WaitFor(database)
    .WaitFor(valkey);

await builder.Build().RunAsync();
