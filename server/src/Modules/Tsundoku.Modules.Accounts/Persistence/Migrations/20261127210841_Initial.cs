using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Tsundoku.Common.Persistence;

#nullable disable

namespace Tsundoku.Modules.Accounts.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261127210841_Initial : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "accounts");

        migrationBuilder.CreateTable(
            name: "data_protection_keys",
            schema: "accounts",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                friendly_name = table.Column<string>(type: "text", nullable: true),
                xml = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_data_protection_keys", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "outbox_messages",
            schema: "accounts",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                payload = table.Column<string>(type: "jsonb", nullable: false),
                occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                attempts = table.Column<int>(type: "integer", nullable: false),
                last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_outbox_messages", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "sessions",
            schema: "accounts",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", maxLength: 128, nullable: false),
                generation = table.Column<int>(type: "integer", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                version = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_sessions", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "user_settings",
            schema: "accounts",
            columns: table => new
            {
                user_id = table.Column<Guid>(type: "uuid", maxLength: 128, nullable: false),
                time_zone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                version = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_user_settings", x => x.user_id);
            });

        migrationBuilder.CreateTable(
            name: "users",
            schema: "accounts",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                normalized_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                password_hash = table.Column<string>(type: "text", nullable: true),
                security_stamp = table.Column<string>(type: "text", nullable: true),
                concurrency_stamp = table.Column<string>(type: "text", nullable: true),
                phone_number = table.Column<string>(type: "text", nullable: true),
                phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                access_failed_count = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_users", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "user_claims",
            schema: "accounts",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                claim_type = table.Column<string>(type: "text", nullable: true),
                claim_value = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_user_claims", x => x.id);
                table.ForeignKey(
                    name: "fk_user_claims_users_user_id",
                    column: x => x.user_id,
                    principalSchema: "accounts",
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "user_logins",
            schema: "accounts",
            columns: table => new
            {
                login_provider = table.Column<string>(type: "text", nullable: false),
                provider_key = table.Column<string>(type: "text", nullable: false),
                provider_display_name = table.Column<string>(type: "text", nullable: true),
                user_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_user_logins", x => new { x.login_provider, x.provider_key });
                table.ForeignKey(
                    name: "fk_user_logins_users_user_id",
                    column: x => x.user_id,
                    principalSchema: "accounts",
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "user_tokens",
            schema: "accounts",
            columns: table => new
            {
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                login_provider = table.Column<string>(type: "text", nullable: false),
                name = table.Column<string>(type: "text", nullable: false),
                value = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_user_tokens", x => new { x.user_id, x.login_provider, x.name });
                table.ForeignKey(
                    name: "fk_user_tokens_users_user_id",
                    column: x => x.user_id,
                    principalSchema: "accounts",
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_outbox_messages_occurred_at",
            schema: "accounts",
            table: "outbox_messages",
            column: "occurred_at",
            filter: "processed_at IS NULL");

        migrationBuilder.CreateIndex(
            name: "ix_sessions_user_id",
            schema: "accounts",
            table: "sessions",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "ix_user_claims_user_id",
            schema: "accounts",
            table: "user_claims",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "ix_user_logins_user_id",
            schema: "accounts",
            table: "user_logins",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "EmailIndex",
            schema: "accounts",
            table: "users",
            column: "normalized_email");

        migrationBuilder.CreateIndex(
            name: "UserNameIndex",
            schema: "accounts",
            table: "users",
            column: "normalized_user_name",
            unique: true);

        // Written by hand: EF cannot map a foreign key from the converted UserId to the Guid key of users.
        foreach (var table in new[] { "sessions", "user_settings" })
        {
            migrationBuilder.AddForeignKey(
                name: $"fk_{table}_users_user_id",
                schema: "accounts",
                table: table,
                column: "user_id",
                principalSchema: "accounts",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        migrationBuilder.GrantAppRole("accounts");
        migrationBuilder.AddOwnerPolicy("accounts", "user_settings", "uuid");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RemoveOwnerPolicy("accounts", "user_settings");
        migrationBuilder.RevokeAppRole("accounts");

        migrationBuilder.DropTable(
            name: "data_protection_keys",
            schema: "accounts");

        migrationBuilder.DropTable(
            name: "outbox_messages",
            schema: "accounts");

        migrationBuilder.DropTable(
            name: "sessions",
            schema: "accounts");

        migrationBuilder.DropTable(
            name: "user_claims",
            schema: "accounts");

        migrationBuilder.DropTable(
            name: "user_logins",
            schema: "accounts");

        migrationBuilder.DropTable(
            name: "user_settings",
            schema: "accounts");

        migrationBuilder.DropTable(
            name: "user_tokens",
            schema: "accounts");

        migrationBuilder.DropTable(
            name: "users",
            schema: "accounts");
    }
}
