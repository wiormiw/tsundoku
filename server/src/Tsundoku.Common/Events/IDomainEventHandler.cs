using Tsundoku.Common.Domain;

namespace Tsundoku.Common.Events;

/// <summary>
/// Reacts to one type of domain event, after the change that raised it commits.
/// The outbox gives each event at least once, so a handler can run two times for one event: make it idempotent (tip 17.06).
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "The name says what it is: a handler of domain events. It is not a .NET event handler.")]
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task Handle(TEvent domainEvent, CancellationToken cancellationToken);
}
