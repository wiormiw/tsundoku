using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tsundoku.Modules.Todos.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261124205255_Initial : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "todos");

        migrationBuilder.CreateTable(
            name: "outbox_messages",
            schema: "todos",
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
            name: "todo_lists",
            schema: "todos",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                owner_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                version = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_todo_lists", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "todo_items",
            schema: "todos",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                todo_list_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_todo_items", x => x.id);
                table.ForeignKey(
                    name: "fk_todo_items_todo_lists_todo_list_id",
                    column: x => x.todo_list_id,
                    principalSchema: "todos",
                    principalTable: "todo_lists",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_outbox_messages_occurred_at",
            schema: "todos",
            table: "outbox_messages",
            column: "occurred_at",
            filter: "processed_at IS NULL");

        migrationBuilder.CreateIndex(
            name: "ix_todo_items_todo_list_id",
            schema: "todos",
            table: "todo_items",
            column: "todo_list_id");

        migrationBuilder.CreateIndex(
            name: "ix_todo_lists_owner_id_created_at",
            schema: "todos",
            table: "todo_lists",
            columns: new[] { "owner_id", "created_at" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "outbox_messages",
            schema: "todos");

        migrationBuilder.DropTable(
            name: "todo_items",
            schema: "todos");

        migrationBuilder.DropTable(
            name: "todo_lists",
            schema: "todos");
    }
}
