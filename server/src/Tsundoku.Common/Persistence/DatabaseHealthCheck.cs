using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Tsundoku.Common.Persistence;

/// <summary>
/// The readiness check of a module's database: can the app open a connection and run a query?
/// It is tagged "ready", never "live": a database that is down must not restart every replica (tip 18.03).
/// </summary>
internal sealed class DatabaseHealthCheck<TDbContext>(TDbContext db) : IHealthCheck where TDbContext : DbContext
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : new HealthCheckResult(context.Registration.FailureStatus, "The database does not answer.");
}
