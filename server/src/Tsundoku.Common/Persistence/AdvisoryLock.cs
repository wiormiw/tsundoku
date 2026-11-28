using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Tsundoku.Common.Persistence;

/// <summary>A Postgres advisory lock that serialises work on one key until the current transaction ends.</summary>
public static class AdvisoryLock
{
    /// <summary>
    /// Waits for the lock on <c>{prefix}:{id}</c>. It must run inside the decorator's transaction: outside one, the lock would be
    /// released as soon as this statement commits, so that throws instead.
    /// </summary>
    public static async Task AcquireAsync(DatabaseFacade database, string prefix, string id, CancellationToken cancellationToken)
    {
        if (database.CurrentTransaction is null) throw new InvalidOperationException($"The advisory lock {prefix} needs a transaction.");
        var key = $"{prefix}:{id}";
        await database.ExecuteSqlAsync($"select pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
    }
}
