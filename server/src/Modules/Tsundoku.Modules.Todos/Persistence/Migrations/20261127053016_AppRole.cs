using Microsoft.EntityFrameworkCore.Migrations;
using Tsundoku.Common.Persistence;

#nullable disable

namespace Tsundoku.Modules.Todos.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261127053016_AppRole : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.GrantAppRole(TodosModule.Schema);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.RevokeAppRole(TodosModule.Schema);
}
