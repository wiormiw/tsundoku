using Microsoft.EntityFrameworkCore.Migrations;

namespace Tsundoku.Common.Persistence;

/// <summary>Names and migration helpers for the database roles and, later, the row-level security policies.</summary>
public static class RowLevelSecurity
{
    /// <summary>The NOLOGIN group the API's login role belongs to. It holds the DML grants, so a table owner is never the API.</summary>
    public const string AppRole = "tsundoku_app";

    /// <summary>The column that holds the owner of a row.</summary>
    public const string OwnerColumn = "user_id";

    /// <summary>
    /// Creates <see cref="AppRole"/> when it is missing and lets it use the tables and sequences of <paramref name="schema"/>,
    /// now and (default privileges, for the migration's own role) later. The schema name is a constant of the module, never user input.
    /// </summary>
    public static void GrantAppRole(this MigrationBuilder migration, string schema) => migration.Sql($"""
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
        """);

    /// <summary>Undoes <see cref="GrantAppRole"/>. The role stays: other schemas and the login role depend on it.</summary>
    public static void RevokeAppRole(this MigrationBuilder migration, string schema) => migration.Sql($"""
        ALTER DEFAULT PRIVILEGES IN SCHEMA "{schema}" REVOKE USAGE, SELECT ON SEQUENCES FROM {AppRole};
        ALTER DEFAULT PRIVILEGES IN SCHEMA "{schema}" REVOKE SELECT, INSERT, UPDATE, DELETE ON TABLES FROM {AppRole};
        REVOKE USAGE, SELECT ON ALL SEQUENCES IN SCHEMA "{schema}" FROM {AppRole};
        REVOKE SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA "{schema}" FROM {AppRole};
        REVOKE USAGE ON SCHEMA "{schema}" FROM {AppRole};
        """);
}
