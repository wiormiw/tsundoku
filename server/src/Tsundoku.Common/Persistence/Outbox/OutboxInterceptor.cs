using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Tsundoku.Common.Domain;

namespace Tsundoku.Common.Persistence.Outbox;

/// <summary>
/// Before each save, moves the domain events of the changed aggregates into outbox rows of the same save.
/// One <c>SaveChangesAsync</c> is one transaction, so the change and its events commit or roll back together.
/// </summary>
internal sealed class OutboxInterceptor(TimeProvider clock) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context) Store(context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } context) Store(context);
        return base.SavingChanges(eventData, result);
    }

    private void Store(DbContext context)
    {
        var now = clock.GetUtcNow();
        var aggregates = context.ChangeTracker.Entries<IAggregateRoot>()
            .Select(entry => entry.Entity)
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToList();
        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                var (type, payload) = OutboxSerializer.Serialize(domainEvent);
                context.Set<OutboxMessage>().Add(new OutboxMessage(type, payload, now));
            }
            aggregate.ClearDomainEvents();
        }
    }
}
