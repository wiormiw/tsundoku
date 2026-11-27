using Microsoft.EntityFrameworkCore;
using Tsundoku.Common.Persistence;
using Tsundoku.Common.Persistence.Outbox;
using Tsundoku.Modules.Todos.Domain;

namespace Tsundoku.Modules.Todos.Persistence;

/// <summary>
/// The EF Core context of the module: schema "todos", with its own outbox table and migration history.
/// Handlers use it directly: commands load an aggregate with the owner in the lookup, queries project to DTOs without tracking.
/// </summary>
internal sealed class TodosDbContext(DbContextOptions<TodosDbContext> options) : DbContext(options)
{
    public DbSet<TodoList> TodoLists => Set<TodoList>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ModulePersistence.ConfigureModel(modelBuilder, TodosModule.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TodosDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ModulePersistence.ConfigureConventions(configurationBuilder);
        TodosConverters.Register(configurationBuilder);
    }
}
