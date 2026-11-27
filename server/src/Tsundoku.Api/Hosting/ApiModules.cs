using Tsundoku.Modules.Todos;

namespace Tsundoku.Api.Hosting;

/// <summary>The list of modules. A new module needs one line in each method.</summary>
internal static class ApiModules
{
    public static WebApplicationBuilder AddModules(this WebApplicationBuilder builder)
    {
        builder.AddTodosModule();
        return builder;
    }

    /// <summary>All HTTP endpoints of the modules, under <c>/api</c>.</summary>
    public static WebApplication MapModules(this WebApplication app)
    {
        var api = app.MapGroup("/api");
        api.MapTodosEndpoints();
        return app;
    }

    public static async Task MigrateModulesAsync(this WebApplication app)
    {
        await app.MigrateTodosAsync();
    }
}
