using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Tsundoku.Common.Domain;
using Tsundoku.Common.Persistence;
using Tsundoku.Modules.Todos.Domain;
using Tsundoku.Modules.Todos.Features;
using Tsundoku.Modules.Todos.Persistence;

namespace Tsundoku.Api.Tests.Integration;

/// <summary>
/// The traps of row-level security, against a scratch table guarded with the same SQL as the migrations. The app side has a pool of one
/// connection, so every statement reuses one backend and a setting left behind by a transaction would show.
/// </summary>
public sealed class RowLevelSecurityTests(ApiFactory factory)
{
    private const string Schema = "rls_probe";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static UserId NewUser() => new(Guid.CreateVersion7().ToString());

    private string OwnerConnectionString
    {
        get
        {
            Assert.SkipWhen(factory.DockerUnavailableReason is not null, factory.DockerUnavailableReason ?? "");
            return factory.OwnerConnectionString;
        }
    }

    private string OneConnection => new NpgsqlConnectionStringBuilder(factory.AppConnectionString)
    {
        MaxPoolSize = 1,
        ApplicationName = "tsundoku-rls-trap",
    }.ConnectionString;

    private DbContext App() => new(new DbContextOptionsBuilder().UseNpgsql(OneConnection).Options);

    private async Task Owner(string sql)
    {
        await using var connection = new NpgsqlConnection(OwnerConnectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }

    /// <summary>Creates the scratch table with rows of <paramref name="owners"/> (one each, under <paramref name="parent"/>), and drops it after the test.</summary>
    private async Task<IAsyncDisposable> Probe(Guid parent, params UserId[] owners)
    {
        var rows = string.Join(", ", owners.Select(owner => $"('{Guid.CreateVersion7()}', '{owner.Value}', '{parent}')"));
        await Owner($"""
            begin;
            drop schema if exists {Schema} cascade;
            create schema {Schema};
            {RowLevelSecurity.GrantAppRoleSql(Schema)}
            create table {Schema}.parents (id uuid primary key);
            create table {Schema}.items (id uuid primary key, user_id uuid not null,
                parent_id uuid not null references {Schema}.parents (id) on delete cascade);
            {RowLevelSecurity.OwnerPolicySql(Schema, "items", "uuid")}
            insert into {Schema}.parents values ('{parent}');
            {(rows.Length > 0 ? $"insert into {Schema}.items values {rows};" : "")}
            commit;
            """);
        return new Dropper(this);
    }

    private sealed class Dropper(RowLevelSecurityTests tests) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() => await tests.Owner($"drop schema if exists {Schema} cascade");
    }

    private static Task<long> Count(DbContext db) =>
        db.Database.SqlQueryRaw<long>($"select count(*) as \"Value\" from {Schema}.items").SingleAsync(Ct);

    private static Task<string?> Setting(DbContext db) =>
        db.Database.SqlQueryRaw<string?>("select current_setting('app.user_id', true) as \"Value\"").SingleAsync(Ct);

    private static Task<int> Backend(DbContext db) =>
        db.Database.SqlQueryRaw<int>("select pg_backend_pid() as \"Value\"").SingleAsync(Ct);

    [Fact]
    public async Task An_owner_read_without_a_user_sees_nothing()
    {
        await using var probe = await Probe(Guid.CreateVersion7(), NewUser(), NewUser());
        await using var db = App();

        (await Count(db)).ShouldBe(0);
    }

    [Fact]
    public async Task An_owner_insert_without_a_user_is_refused()
    {
        var parent = Guid.CreateVersion7();
        await using var probe = await Probe(parent);
        await using var db = App();

        var refused = await Should.ThrowAsync<PostgresException>(() => db.Database.ExecuteSqlAsync(
            $"insert into rls_probe.items values ({Guid.CreateVersion7()}, {Guid.CreateVersion7()}, {parent})", Ct));
        refused.SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
    }

    [Fact]
    public async Task A_user_transaction_sees_only_its_rows()
    {
        var a = NewUser();
        await using var probe = await Probe(Guid.CreateVersion7(), a, NewUser());
        await using var db = App();

        await using var transaction = await RowLevelSecurity.BeginTransactionAsync(db.Database, a, Ct);
        (await Count(db)).ShouldBe(1);
        (await db.Database.SqlQueryRaw<Guid>($"select user_id as \"Value\" from {Schema}.items").SingleAsync(Ct)).ShouldBe(Guid.Parse(a.Value));
    }

    [Fact]
    public async Task A_user_cannot_insert_or_move_a_row_to_another_users_id()
    {
        var (a, b) = (NewUser(), NewUser());
        var parent = Guid.CreateVersion7();
        await using var probe = await Probe(parent, a);
        var other = Guid.Parse(b.Value);

        async Task RefusedAs(UserId user, FormattableString sql)
        {
            await using var db = App();
            await using var transaction = await RowLevelSecurity.BeginTransactionAsync(db.Database, user, Ct);
            var refused = await Should.ThrowAsync<PostgresException>(() => db.Database.ExecuteSqlAsync(sql, Ct));
            refused.SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
        }

        // WITH CHECK: the new row must belong to the transaction's user, on insert and on update alike.
        await RefusedAs(a, $"insert into rls_probe.items values ({Guid.CreateVersion7()}, {other}, {parent})");
        await RefusedAs(a, $"update rls_probe.items set user_id = {other}");
    }

    [Fact]
    public async Task The_setting_is_empty_after_commit_rollback_and_cancel_on_the_same_connection()
    {
        var a = NewUser();
        var parent = Guid.CreateVersion7();
        await using var probe = await Probe(parent, a);
        int backend;
        await using (var db = App()) backend = await Backend(db);

        async Task ShowsNothingAfterwards()
        {
            await using var db = App();
            (await Backend(db)).ShouldBe(backend, "the pool of one must reuse the same backend");
            (await Setting(db)).ShouldBe("");
            (await Count(db)).ShouldBe(0);
        }

        await using (var db = App())
        {
            await using var transaction = await RowLevelSecurity.BeginTransactionAsync(db.Database, a, Ct);
            (await Count(db)).ShouldBe(1);
            await transaction.CommitAsync(Ct);
        }
        await ShowsNothingAfterwards();

        await using (var db = App())
        {
            await using var transaction = await RowLevelSecurity.BeginTransactionAsync(db.Database, a, Ct);
            await db.Database.ExecuteSqlAsync($"insert into rls_probe.items values ({Guid.CreateVersion7()}, {Guid.Parse(a.Value)}, {parent})", Ct);
            await transaction.RollbackAsync(Ct);
        }
        await ShowsNothingAfterwards();

        await using (var db = App())
        {
            await using var transaction = await RowLevelSecurity.BeginTransactionAsync(db.Database, a, Ct);
            using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Ct);
            cancel.CancelAfter(TimeSpan.FromMilliseconds(100));
            await Should.ThrowAsync<OperationCanceledException>(() => db.Database.ExecuteSqlRawAsync("select pg_sleep(5)", cancel.Token));
        }
        await ShowsNothingAfterwards();
    }

    [Fact]
    public async Task A_cascade_from_a_parent_deletes_owned_rows_whatever_the_setting()
    {
        var parent = Guid.CreateVersion7();
        await using var probe = await Probe(parent, NewUser(), NewUser());
        await using (var db = App())
        {
            (await db.Database.ExecuteSqlAsync($"delete from rls_probe.parents where id = {parent}", Ct)).ShouldBe(1);
        }

        await using var connection = new NpgsqlConnection(OwnerConnectionString);
        await connection.OpenAsync(Ct);
        // The owner of the container is a superuser, so it sees every row despite FORCE.
        await using var count = new NpgsqlCommand($"select count(*) from {Schema}.items", connection);
        ((long)(await count.ExecuteScalarAsync(Ct))!).ShouldBe(0);
    }

    [Fact]
    public async Task Rls_alone_hides_another_users_todo_list()
    {
        _ = OwnerConnectionString;
        var a = new UserId($"user-{Guid.NewGuid():N}");
        var b = new UserId($"user-{Guid.NewGuid():N}");
        using var client = factory.CreateApiClient(a.Value);
        var created = await client.PostAsJsonAsync("/api/todo-lists", new { title = "Mine" }, Ct);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = new TodoListId((await created.Content.ReadFromJsonAsync<IdResponse>(Ct)).ShouldNotBeNull().Id);

        async Task<int> Visible(UserId user)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<TodosDbContext>();
            await using var transaction = await RowLevelSecurity.BeginTransactionAsync(db.Database, user, Ct);
            // No owner filter on purpose: only the policy stands between B and A's list.
            return await db.TodoLists.AsNoTracking().Where(list => list.Id == id).CountAsync(Ct);
        }

        (await Visible(b)).ShouldBe(0);
        (await Visible(a)).ShouldBe(1);
    }
}
