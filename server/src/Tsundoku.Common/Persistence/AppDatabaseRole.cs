using System.Data.Common;
using Npgsql;

namespace Tsundoku.Common.Persistence;

/// <summary>The login role the API connects as: a member of <see cref="RowLevelSecurity.AppRole"/>, never the owner.</summary>
public static class AppDatabaseRole
{
    /// <summary>
    /// Creates or updates, as the owner, the login role named in <paramref name="appConnectionString"/> with its password.
    /// Safe to run any number of times, so it works on an existing volume too.
    /// </summary>
    public static async Task EnsureLoginAsync(string ownerConnectionString, string appConnectionString, CancellationToken cancellationToken)
    {
        var owner = new NpgsqlConnectionStringBuilder(ownerConnectionString);
        var app = new NpgsqlConnectionStringBuilder(appConnectionString);
        if (string.IsNullOrEmpty(app.Username) || string.IsNullOrEmpty(app.Password))
            throw new InvalidOperationException("The app connection string needs a Username and a Password.");
        // A superuser or owner as the API would ignore row-level security, and demoting it would break the database.
        if (string.Equals(owner.Username, app.Username, StringComparison.Ordinal))
            throw new InvalidOperationException($"The app role '{app.Username}' is the owner role; the API must connect as a separate login.");

        await using var connection = new NpgsqlConnection(owner.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        // Postgres quotes the name (%I) and the password (%L): `create role` takes no bind parameters.
        await using var plan = new NpgsqlCommand($"""
            select exists (select from pg_roles where rolname = @name),
                   format('create role %I login password %L in role {RowLevelSecurity.AppRole}', @name, @password),
                   format('alter role %I login password %L nosuperuser nobypassrls', @name, @password),
                   format('grant {RowLevelSecurity.AppRole} to %I', @name)
            """, connection);
        plan.Parameters.AddWithValue("name", app.Username);
        plan.Parameters.AddWithValue("password", app.Password);
        string[] statements;
        await using (var reader = await plan.ExecuteReaderAsync(cancellationToken))
        {
            await reader.ReadAsync(cancellationToken);
            statements = reader.GetBoolean(0)
                ? [reader.GetString(2), reader.GetString(3)]
                : [reader.GetString(1)];
        }

        foreach (var statement in statements)
        {
            await using var command = new NpgsqlCommand(statement, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    /// <summary>
    /// <c>null</c> when <c>current_user</c> is not a superuser, not BYPASSRLS and owns no table, so row-level security applies.
    /// Otherwise a sentence that names the role.
    /// </summary>
    internal static async Task<string?> UnsafeRoleReasonAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select current_user::text, r.rolsuper, r.rolbypassrls,
                   exists (select from pg_tables where tableowner = current_user
                           and schemaname not in ('pg_catalog', 'information_schema'))
            from pg_roles r where r.rolname = current_user
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        var role = reader.GetString(0);
        if (reader.GetBoolean(1)) return $"The database role '{role}' is a superuser, so row-level security would not apply.";
        if (reader.GetBoolean(2)) return $"The database role '{role}' has BYPASSRLS, so row-level security would not apply.";
        if (reader.GetBoolean(3)) return $"The database role '{role}' owns tables, so it can switch row-level security off.";
        return null;
    }
}
