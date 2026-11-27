using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

[assembly: AssemblyFixture(typeof(Tsundoku.Api.Tests.Integration.ApiFactory))]

namespace Tsundoku.Api.Tests.Integration;

/// <summary>
/// The real API with a temporary Postgres and Valkey in Docker. It migrates at startup, the same as Development.
/// When Docker is not available, the integration tests are skipped. They do not fail.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private RedisContainer? _valkey;

    public string? DockerUnavailableReason { get; private set; }

    public async ValueTask InitializeAsync()
    {
        try
        {
            // Keep the tag the same as the AppHost.
            _postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();
            // Valkey speaks the Redis protocol; keep the tag the same as the AppHost.
            _valkey = new RedisBuilder("valkey/valkey:8-alpine").Build();
            await Task.WhenAll(_postgres.StartAsync(), _valkey.StartAsync());
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            DockerUnavailableReason = $"Docker is not available ({exception.GetType().Name}); start it to run integration tests.";
            return;
        }

        // Start the server (and its migrations) one time, before the tests run in parallel.
        using var warmUp = CreateClient();
    }

    /// <summary>A client that signs in as <paramref name="user"/>, or an anonymous client.</summary>
    public HttpClient CreateApiClient(string? user = "alice")
    {
        Assert.SkipWhen(DockerUnavailableReason is not null, DockerUnavailableReason ?? "");
        var client = CreateClient();
        if (user is not null) client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, user);
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:appdb", _postgres!.GetConnectionString());
        builder.UseSetting("ConnectionStrings:valkey", _valkey!.GetConnectionString());
        builder.UseSetting("Database:MigrateOnStartup", "true");
        // The JWT options must be valid at startup, but the tests sign in with TestAuthHandler.
        builder.UseSetting("Authentication:Schemes:Bearer:Authority", "https://identity.invalid/");
        builder.UseSetting("ApiLimits:RequestsPerMinute", "100000");
        // The outbox reads often, so the tests see the events quickly.
        builder.UseSetting("Outbox:PollInterval", "00:00:00.050");
        builder.ConfigureTestServices(services => services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { }));
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (_postgres is not null) await _postgres.DisposeAsync();
        if (_valkey is not null) await _valkey.DisposeAsync();
    }
}
