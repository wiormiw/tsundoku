using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Tsundoku.Modules.Todos.Persistence;

/// <summary>Makes the context for <c>dotnet ef</c>. It builds the model without a database or a host.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TodosDbContext>
{
    public TodosDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<TodosDbContext>().UseNpgsql("Host=localhost;Database=appdb").Options);
}
