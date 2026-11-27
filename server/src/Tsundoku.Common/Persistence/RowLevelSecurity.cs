using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Tsundoku.Common.Domain;

namespace Tsundoku.Common.Persistence;

/// <summary>Names and migration helpers for the database roles and the row-level security policies, and the one place that sets the user of a transaction.</summary>
public static class RowLevelSecurity
{
    /// <summary>The NOLOGIN group the API's login role belongs to. It holds the DML grants, so a table owner is never the API.</summary>
    public const string AppRole = "tsundoku_app";

    /// <summary>The column that holds the owner of a row.</summary>
    public const string OwnerColumn = "user_id";

    /// <summary>
    /// Begins a transaction and, with a user, makes it that user's: the owner policies compare <see cref="OwnerColumn"/> with this setting.
    /// <c>set_config(..., true)</c> is transaction-local, so Postgres clears it at commit or rollback whatever the pool does,
    /// and the id is a bound parameter.
    /// </summary>
    internal static async Task<IDbContextTransaction> BeginTransactionAsync(DatabaseFacade database, UserId? user, CancellationToken cancellationToken)
    {
        var transaction = await database.BeginTransactionAsync(cancellationToken);
        if (user is not { } id) return transaction;
        try
        {
            await database.ExecuteSqlAsync($"select set_config('app.user_id', {id.Value}, true)", cancellationToken);
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Creates <see cref="AppRole"/> when it is missing and lets it use the tables and sequences of <paramref name="schema"/>,
    /// now and (default privileges, for the migration's own role) later. The schema name is a constant of the module, never user input.
    /// </summary>
    public static void GrantAppRole(this MigrationBuilder migration, string schema) => migration.Sql(GrantAppRoleSql(schema));

    internal static string GrantAppRoleSql(string schema) => $"""
        DO $$
        BEGIN
            IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = '{AppRole}') THEN
                CREATE ROLE {AppRole} NOLOGIN;
            END IF;
        END
        $$;
        GRANT USAGE ON SCHEMA "{schema}" TO {AppRole};
        GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA "{schema}" TO {AppRole};
        GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA "{schema}" TO {AppRole};
        ALTER DEFAULT PRIVILEGES IN SCHEMA "{schema}" GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {AppRole};
        ALTER DEFAULT PRIVILEGES IN SCHEMA "{schema}" GRANT USAGE, SELECT ON SEQUENCES TO {AppRole};
        """;

    /// <summary>Undoes <see cref="GrantAppRole"/>. The role stays: other schemas and the login role depend on it.</summary>
    public static void RevokeAppRole(this MigrationBuilder migration, string schema) => migration.Sql($"""
        ALTER DEFAULT PRIVILEGES IN SCHEMA "{schema}" REVOKE USAGE, SELECT ON SEQUENCES FROM {AppRole};
        ALTER DEFAULT PRIVILEGES IN SCHEMA "{schema}" REVOKE SELECT, INSERT, UPDATE, DELETE ON TABLES FROM {AppRole};
        REVOKE USAGE, SELECT ON ALL SEQUENCES IN SCHEMA "{schema}" FROM {AppRole};
        REVOKE SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA "{schema}" FROM {AppRole};
        REVOKE USAGE ON SCHEMA "{schema}" FROM {AppRole};
        """);

    /// <summary>
    /// Forces row-level security on an owned table (the owner too is bound, FORCE) with one policy: the app role sees and writes only
    /// the rows of the transaction's user. No user means no rows: the setting is <c>''</c>, not NULL, after a transaction, hence <c>nullif</c>.
    /// </summary>
    public static void AddOwnerPolicy(this MigrationBuilder migration, string schema, string table, string ownerColumnType) =>
        migration.Sql(OwnerPolicySql(schema, table, ownerColumnType));

    /// <summary>Undoes <see cref="AddOwnerPolicy"/>.</summary>
    public static void RemoveOwnerPolicy(this MigrationBuilder migration, string schema, string table) => migration.Sql($"""
        DROP POLICY IF EXISTS {table}_owner ON {schema}.{table};
        ALTER TABLE {schema}.{table} NO FORCE ROW LEVEL SECURITY;
        ALTER TABLE {schema}.{table} DISABLE ROW LEVEL SECURITY;
        """);

    internal static string OwnerPolicySql(string schema, string table, string ownerColumnType) => $"""
        ALTER TABLE {schema}.{table} ENABLE ROW LEVEL SECURITY;
        ALTER TABLE {schema}.{table} FORCE ROW LEVEL SECURITY;
        CREATE POLICY {table}_owner ON {schema}.{table} FOR ALL TO {AppRole}
          USING ({OwnerColumn} = nullif(current_setting('app.user_id', true), '')::{ownerColumnType})
          WITH CHECK ({OwnerColumn} = nullif(current_setting('app.user_id', true), '')::{ownerColumnType});
        """;
}
