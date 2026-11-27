using Microsoft.EntityFrameworkCore;
using Tsundoku.Common.Persistence.Outbox;

namespace Tsundoku.Common.Persistence;

/// <summary>
/// What each module's database needs: one context, one schema, its own migration history and its own outbox.
/// All modules share one PostgreSQL database and one connection string.
/// </summary>
public static class ModulePersistence
{
    /// <summary>The name of the connection string. It must be the same as the database resource name in the AppHost.</summary>
    public const string ConnectionStringName = "appdb";

    /// <summary>Registers the context, its outbox processor and its readiness check.</summary>
    public static IHostApplicationBuilder AddModuleDatabase<TDbContext>(this IHostApplicationBuilder builder, string schema)
        where TDbContext : DbContext
    {
        var connectionString = builder.Configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");
        var services = builder.Services;

        services.AddSingleton<AuditableEntityInterceptor>();
        services.AddSingleton<OutboxInterceptor>();
        // No EnableRetryOnFailure: the outbox processor starts its own transactions, and a retrying
        // execution strategy refuses those. Retry at one layer only (tip 15.05).
        services.AddDbContext<TDbContext>((provider, options) => options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", schema))
            .AddInterceptors(
                provider.GetRequiredService<AuditableEntityInterceptor>(),
                provider.GetRequiredService<OutboxInterceptor>()));

        // The outbox: events go out after the commit, at least once. The options are shared by all modules.
        services.AddOptions<OutboxOptions>().BindConfiguration(OutboxOptions.Section).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<OutboxSerializer>();
        services.AddHostedService<OutboxProcessor<TDbContext>>();

        services.AddHealthChecks().AddCheck<DatabaseHealthCheck<TDbContext>>($"database-{schema}", tags: ["ready"]);
        return builder;
    }

    /// <summary>For <c>ConfigureConventions</c> of a module context: snake_case names and the shared converters.</summary>
    public static void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Conventions.Add(_ => new SnakeCaseNamingConvention());
        ValueObjectConverters.Register(configurationBuilder);
    }

    /// <summary>For <c>OnModelCreating</c> of a module context: the schema, and the outbox table inside it.</summary>
    public static void ConfigureModel(ModelBuilder modelBuilder, string schema)
    {
        modelBuilder.HasDefaultSchema(schema);
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
    }

    /// <summary>
    /// Applies the pending migrations of one module when <c>Database:MigrateOnStartup</c> is true (Development, tests).
    /// In production, run a migration bundle as a separate deploy step, with expand/contract changes (tip 28.01).
    /// </summary>
    public static async Task MigrateAsync<TDbContext>(this IHost host) where TDbContext : DbContext
    {
        if (!host.Services.GetRequiredService<IConfiguration>().GetValue<bool>("Database:MigrateOnStartup")) return;
        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<TDbContext>().Database.MigrateAsync();
    }
}
