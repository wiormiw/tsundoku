using Tsundoku.Common.Persistence;
using Tsundoku.Modules.Accounts;
using Tsundoku.Modules.Todos;

namespace Tsundoku.Api.Hosting;

/// <summary>The list of modules. A new module needs one line in each method.</summary>
internal static class ApiModules
{
    public static WebApplicationBuilder AddModules(this WebApplicationBuilder builder)
    {
        builder.AddAccountsModule();
        builder.AddTodosModule();
        return builder;
    }

    /// <summary>All HTTP endpoints of the modules, under <c>/api</c>.</summary>
    public static WebApplication MapModules(this WebApplication app)
    {
        var api = app.MapGroup("/api");
        api.MapAccountsEndpoints();
        api.MapTodosEndpoints();
        return app;
    }

    /// <summary>Migrates every module as the owner, then makes sure the API's login role exists with its password.</summary>
    public static async Task MigrateModulesAsync(this WebApplication app)
    {
        await app.MigrateAccountsAsync();
        await app.MigrateTodosAsync();

        var connectionStrings = app.Configuration;
        var owner = connectionStrings.GetConnectionString(ModulePersistence.OwnerConnectionStringName)!;
        var login = connectionStrings.GetConnectionString(ModulePersistence.ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ModulePersistence.ConnectionStringName}' is not configured.");
        await AppDatabaseRole.EnsureLoginAsync(owner, login, app.Lifetime.ApplicationStopping);
    }
}
