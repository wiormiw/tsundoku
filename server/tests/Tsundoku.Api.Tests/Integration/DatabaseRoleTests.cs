using System.Net;
using Npgsql;
using Tsundoku.Common.Persistence;

namespace Tsundoku.Api.Tests.Integration;

public sealed class DatabaseRoleTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<T> Scalar<T>(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync(Ct))!;
    }

    private string Owner
    {
        get
        {
            Assert.SkipWhen(factory.DockerUnavailableReason is not null, factory.DockerUnavailableReason ?? "");
            return factory.OwnerConnectionString;
        }
    }

    [Fact]
    public async Task The_app_role_is_neither_superuser_nor_bypassrls_nor_owner()
    {
        var owner = Owner;
        await using var connection = new NpgsqlConnection(owner);
        await connection.OpenAsync(Ct);
        await using var flags = new NpgsqlCommand("select rolsuper, rolbypassrls from pg_roles where rolname = 'tsundoku_api'", connection);
        await using (var reader = await flags.ExecuteReaderAsync(Ct))
        {
            (await reader.ReadAsync(Ct)).ShouldBeTrue();
            reader.GetBoolean(0).ShouldBeFalse();
            reader.GetBoolean(1).ShouldBeFalse();
        }

        (await Scalar<bool>(owner, "select pg_has_role('tsundoku_api', 'tsundoku_app', 'member')")).ShouldBeTrue();
        (await Scalar<long>(owner, "select count(*) from pg_tables where tableowner in ('tsundoku_api','tsundoku_app')")).ShouldBe(0);
    }

    [Fact]
    public async Task The_api_connects_as_the_app_role()
    {
        _ = Owner;
        (await Scalar<string>(factory.AppConnectionString, "select current_user::text")).ShouldBe("tsundoku_api");
    }

    [Fact]
    public async Task Ensuring_the_login_twice_succeeds_and_keeps_the_password()
    {
        var owner = Owner;
        await AppDatabaseRole.EnsureLoginAsync(owner, factory.AppConnectionString, Ct);
        await AppDatabaseRole.EnsureLoginAsync(owner, factory.AppConnectionString, Ct);
        (await Scalar<string>(factory.AppConnectionString, "select current_user::text")).ShouldBe("tsundoku_api");
    }

    [Fact]
    public async Task A_password_with_a_quote_and_a_percent_works()
    {
        var owner = Owner;
        var probe = new NpgsqlConnectionStringBuilder(owner) { Username = "tsundoku_quote_probe", Password = "it's 100%", Pooling = false }.ConnectionString;
        try
        {
            await AppDatabaseRole.EnsureLoginAsync(owner, probe, Ct);
            (await Scalar<string>(probe, "select current_user::text")).ShouldBe("tsundoku_quote_probe");
        }
        finally
        {
            await using var connection = new NpgsqlConnection(owner);
            await connection.OpenAsync(CancellationToken.None);
            await using var drop = new NpgsqlCommand("drop role if exists tsundoku_quote_probe", connection);
            await drop.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task The_owner_as_app_role_is_refused()
    {
        var owner = Owner;
        await Should.ThrowAsync<InvalidOperationException>(AppDatabaseRole.EnsureLoginAsync(owner, owner, Ct));
    }

    [Fact]
    public async Task Readiness_fails_when_the_api_connects_as_the_owner()
    {
        var owner = Owner;
        // No migration at startup: EnsureLoginAsync would refuse the owner as the app role first.
        await using var unsafeFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:appdb", owner);
            builder.UseSetting("Database:MigrateOnStartup", "false");
        });
        using var client = unsafeFactory.CreateClient();
        (await client.GetAsync("/health/ready", Ct)).StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task The_todos_migration_grants_the_app_role() =>
        (await Scalar<bool>(Owner, "select has_table_privilege('tsundoku_app', 'todos.todo_lists', 'INSERT')")).ShouldBeTrue();
}
