using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tsundoku.Modules.Accounts.Features;
using Tsundoku.Modules.Accounts.Persistence;
using System.Security.Cryptography;
using Npgsql;
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

    /// <summary>Every mail the API sent. It replaces the log mailer, so a test can follow a confirmation link.</summary>
    internal CapturingEmailSender Mail { get; } = new();

    public string? DockerUnavailableReason { get; private set; }

    /// <summary>The container's own (owner) connection string: migrations and test set-up.</summary>
    public string OwnerConnectionString => _postgres!.GetConnectionString();

    private readonly string _appPassword = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));

    /// <summary>The API's connection string: the same server as the owner's, as the login role <c>tsundoku_api</c> with a random password for each run.</summary>
    public string AppConnectionString => new NpgsqlConnectionStringBuilder(OwnerConnectionString)
    {
        Username = "tsundoku_api",
        Password = _appPassword,
    }.ConnectionString;

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
        builder.UseSetting("ConnectionStrings:appdb-owner", OwnerConnectionString);
        builder.UseSetting("ConnectionStrings:appdb", AppConnectionString);
        builder.UseSetting("ConnectionStrings:valkey", _valkey!.GetConnectionString());
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Accounts:PublicBaseUrl", "http://localhost");
        builder.UseSetting("Accounts:SignUpEnabled", "true");
        builder.UseSetting("ApiLimits:RequestsPerMinute", "100000");
        // The outbox reads often, so the tests see the events quickly.
        builder.UseSetting("Outbox:PollInterval", "00:00:00.050");
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            services.RemoveAll<IAccountMailer>();
            services.RemoveAll<IEmailSender<AccountUser>>();
            services.AddSingleton<IAccountMailer>(Mail);
            services.AddSingleton<IEmailSender<AccountUser>>(Mail);
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (_postgres is not null) await _postgres.DisposeAsync();
        if (_valkey is not null) await _valkey.DisposeAsync();
    }
}
