using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Tsundoku.Common.Domain;

namespace Tsundoku.Common.Persistence;

/// <summary>
/// Writes <c>CreatedAt</c> and <c>UpdatedAt</c> from <see cref="TimeProvider"/> at each save.
/// They are on each aggregate root. The domain shows them as read-only, so the interceptor writes them through the property entries of EF.
/// </summary>
internal sealed class AuditableEntityInterceptor(TimeProvider clock) : SaveChangesInterceptor
{
    private const string CreatedAt = nameof(AggregateRoot<>.CreatedAt);
    private const string UpdatedAt = nameof(AggregateRoot<>.UpdatedAt);

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context) Stamp(context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } context) Stamp(context);
        return base.SavingChanges(eventData, result);
    }

    private void Stamp(DbContext context)
    {
        // Postgres keeps microseconds, and .NET has ticks of 100 ns. Thus round the value here.
        // Then the value in the response after a save is the same as the value that all later reads return.
        var now = clock.GetUtcNow();
        now = now.AddTicks(-(now.Ticks % (TimeSpan.TicksPerMillisecond / 1000)));
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Metadata.FindProperty(CreatedAt) is null) continue;
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Property(CreatedAt).CurrentValue = now;
                    break;
                case EntityState.Modified:
                    entry.Property(UpdatedAt).CurrentValue = now;
                    break;
            }
        }
    }
}
