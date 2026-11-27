using Microsoft.EntityFrameworkCore.Migrations;
using Tsundoku.Common.Persistence;

#nullable disable

namespace Tsundoku.Modules.Todos.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261127053510_RowLevelSecurity : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "owner_id",
            schema: "todos",
            table: "todo_lists",
            newName: "user_id");

        migrationBuilder.RenameIndex(
            name: "ix_todo_lists_owner_id_created_at",
            schema: "todos",
            table: "todo_lists",
            newName: "ix_todo_lists_user_id_created_at");

        // OwnerId is text: the sub claim of the identity provider.
        migrationBuilder.AddOwnerPolicy(TodosModule.Schema, "todo_lists", "text");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RemoveOwnerPolicy(TodosModule.Schema, "todo_lists");

        migrationBuilder.RenameColumn(
            name: "user_id",
            schema: "todos",
            table: "todo_lists",
            newName: "owner_id");

        migrationBuilder.RenameIndex(
            name: "ix_todo_lists_user_id_created_at",
            schema: "todos",
            table: "todo_lists",
            newName: "ix_todo_lists_owner_id_created_at");
    }
}
