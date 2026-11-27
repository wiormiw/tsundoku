using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Tsundoku.Common.Persistence;

/// <summary>
/// The readiness check of a module's database: can the app open a connection and run a query, and is the role one
/// that row-level security applies to? It is tagged "ready", never "live": a database that is down must not restart every replica (tip 18.03).
/// The first safe answer is kept for the process (a role does not change while it runs); an unsafe one is checked again and logged once.
/// </summary>
internal sealed partial class DatabaseHealthCheck<TDbContext>(
    TDbContext db, DatabaseRoleVerdict verdict, ILogger<DatabaseHealthCheck<TDbContext>> logger) : IHealthCheck where TDbContext : DbContext
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!await db.Database.CanConnectAsync(cancellationToken))
            return new HealthCheckResult(context.Registration.FailureStatus, "The database does not answer.");
        if (verdict.IsSafe) return HealthCheckResult.Healthy();

        var connection = db.Database.GetDbConnection();
        var opened = connection.State != System.Data.ConnectionState.Open;
        if (opened) await connection.OpenAsync(cancellationToken);
        try
        {
            var reason = await AppDatabaseRole.UnsafeRoleReasonAsync(connection, cancellationToken);
            if (reason is null)
            {
                verdict.MarkSafe();
                return HealthCheckResult.Healthy();
            }

            if (verdict.ClaimLog()) LogUnsafeRole(reason);
            return HealthCheckResult.Unhealthy(reason);
        }
        finally
        {
            if (opened) await connection.CloseAsync();
        }
    }

    [LoggerMessage(EventId = 200, Level = LogLevel.Error, Message = "{Reason}")]
    private partial void LogUnsafeRole(string reason);
}
