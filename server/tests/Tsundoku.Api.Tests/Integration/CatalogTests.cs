using System.Collections.Frozen;
using Npgsql;
using Tsundoku.Common.Persistence;

namespace Tsundoku.Api.Tests.Integration;

/// <summary>
/// The catalog after all migrations, read as the owner: every table with an owner column is guarded by forced row-level security and
/// an owner policy, and the app role can use every module table. A new owned table fails here until its migration adds the policy.
/// </summary>
public sealed class CatalogTests(ApiFactory factory)
{
    /// <summary><c>schema.table</c> names of tables with a <c>user_id</c> that are read before a user is known, so they carry no policy.</summary>
    internal static readonly FrozenSet<string> ExemptTables = FrozenSet.Create<string>(StringComparer.Ordinal,
        ["accounts.user_claims", "accounts.user_logins", "accounts.user_tokens", "accounts.sessions"]);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    /// Tables outside the system schemas with an owner column that are not guarded: forced RLS, and the owner policy as the only
    /// permissive one (permissive policies OR together, so any other would widen it). That policy is for every command, to the app role
    /// only, and its USING and WITH CHECK are both exactly the owner column compared with the setting read through nullif.
    /// </summary>
    private const string UnguardedTables = $$"""
        select n.nspname || '.' || c.relname
        from pg_class c
        join pg_namespace n on n.oid = c.relnamespace
        where c.relkind in ('r', 'p')
          and n.nspname not in ('pg_catalog', 'information_schema') and n.nspname not like 'pg\_%'
          and exists (select from pg_attribute a where a.attrelid = c.oid and a.attname = '{{RowLevelSecurity.OwnerColumn}}' and not a.attisdropped)
          and not (c.relrowsecurity and c.relforcerowsecurity
              and (select count(*) from pg_policy p where p.polrelid = c.oid and p.polpermissive) = 1
              and exists (
                  select from pg_policy p
                  where p.polrelid = c.oid and p.polpermissive
                    and p.polcmd = '*'
                    and p.polroles = array[(select oid from pg_roles where rolname = '{{RowLevelSecurity.AppRole}}')]::oid[]
                    and pg_get_expr(p.polqual, p.polrelid) = pg_get_expr(p.polwithcheck, p.polrelid)
                    and pg_get_expr(p.polqual, p.polrelid)
                        ~ '^\(\(?{{RowLevelSecurity.OwnerColumn}}\)?(::[\w ]+)? = \(?NULLIF\(current_setting\(''app\.user_id''::text, true\), ''''::text\)\)?(::[\w ]+)?\)$'))
        order by 1
        """;

    private string Owner
    {
        get
        {
            Assert.SkipWhen(factory.DockerUnavailableReason is not null, factory.DockerUnavailableReason ?? "");
            return factory.OwnerConnectionString;
        }
    }

    private async Task<List<string>> Rows(string sql)
    {
        await using var connection = new NpgsqlConnection(Owner);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        var rows = new List<string>();
        while (await reader.ReadAsync(Ct)) rows.Add(reader.IsDBNull(0) ? "" : reader.GetString(0));
        return rows;
    }

    private async Task Execute(string sql)
    {
        await using var connection = new NpgsqlConnection(Owner);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Every_owned_table_forces_rls_with_an_owner_policy() =>
        (await Rows(UnguardedTables)).Where(table => !ExemptTables.Contains(table)).ShouldBeEmpty();

    [Fact]
    public async Task Every_exempt_table_exists()
    {
        foreach (var table in ExemptTables)
            (await Rows($"select to_regclass('{table}')::text")).ShouldBe([table], $"{table} is exempt but does not exist");
    }

    [Fact]
    public async Task The_check_flags_an_unguarded_table()
    {
        await Execute("drop schema if exists catalog_probe cascade; create schema catalog_probe; create table catalog_probe.t (user_id uuid)");
        try
        {
            (await Rows(UnguardedTables)).ShouldContain("catalog_probe.t");
        }
        finally
        {
            await Execute("drop schema if exists catalog_probe cascade");
        }
    }

    /// <summary>A probe table guarded by the migrations' own SQL passes; each way to widen or skip its policy is flagged.</summary>
    [Theory]
    [InlineData("", false)]
    [InlineData("create policy wide on catalog_probe.t for select to tsundoku_app using (true);", true)]
    [InlineData("create policy narrow on catalog_probe.t as restrictive for all to tsundoku_app using (true);", false)]
    [InlineData("alter policy t_owner on catalog_probe.t to public;", true)]
    [InlineData("alter policy t_owner on catalog_probe.t with check (true);", true)]
    [InlineData("alter policy t_owner on catalog_probe.t using (user_id is not null);", true)]
    [InlineData("alter table catalog_probe.t no force row level security;", true)]
    public async Task The_check_flags_a_widened_or_partial_policy(string change, bool flagged)
    {
        await Execute($"""
            drop schema if exists catalog_probe cascade;
            create schema catalog_probe;
            create table catalog_probe.t (user_id uuid);
            {RowLevelSecurity.OwnerPolicySql("catalog_probe", "t", "uuid")}
            {change}
            """);
        try
        {
            (await Rows(UnguardedTables)).Contains("catalog_probe.t").ShouldBe(flagged, change);
        }
        finally
        {
            await Execute("drop schema if exists catalog_probe cascade");
        }
    }

    [Fact]
    public async Task The_app_role_has_dml_on_every_table() =>
        // A module schema is one with its own migration history.
        (await Rows($"""
            select t.schemaname || '.' || t.tablename
            from pg_tables t
            where t.schemaname in (select schemaname from pg_tables where tablename = '__ef_migrations_history')
              and not (has_table_privilege('{RowLevelSecurity.AppRole}', format('%I.%I', t.schemaname, t.tablename), 'SELECT')
                   and has_table_privilege('{RowLevelSecurity.AppRole}', format('%I.%I', t.schemaname, t.tablename), 'INSERT')
                   and has_table_privilege('{RowLevelSecurity.AppRole}', format('%I.%I', t.schemaname, t.tablename), 'UPDATE')
                   and has_table_privilege('{RowLevelSecurity.AppRole}', format('%I.%I', t.schemaname, t.tablename), 'DELETE'))
            """)).ShouldBeEmpty();
}
