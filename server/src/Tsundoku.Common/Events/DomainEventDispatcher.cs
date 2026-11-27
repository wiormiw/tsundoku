using Tsundoku.Common.Domain;

namespace Tsundoku.Common.Events;

/// <summary>
/// Runs the handlers of one committed event. The outbox processor in Infrastructure calls it.
/// Each handler runs in its own DI scope, so it gets its own unit of work.
/// When a handler throws, the exception goes to the outbox processor, which tries the message again later.
/// </summary>
public sealed class DomainEventDispatcher(DomainEventRegistry registry, IServiceScopeFactory scopes)
{
    public async Task Dispatch(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        foreach (var handle in registry.HandlersOf(domainEvent.GetType()))
        {
            await using var scope = scopes.CreateAsyncScope();
            await handle(scope.ServiceProvider, domainEvent, cancellationToken);
        }
    }
}
