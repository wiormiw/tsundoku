namespace Tsundoku.Common.Domain;

/// <summary>
/// The root of an aggregate. It keeps the domain events of its changes until persistence writes them to the outbox,
/// and a version that goes up at each change. The database uses the version as the concurrency token,
/// so a change to any part of the aggregate conflicts with a parallel change to any other part.
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot where TId : struct, IEquatable<TId>
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public int Version { get; private set; }

    /// <summary>The save pipeline writes this value one time, at insert.</summary>
    public DateTimeOffset CreatedAt { get; private init; }

    /// <summary>The save pipeline writes this value at each update.</summary>
    public DateTimeOffset? UpdatedAt { get; private set; }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>Call it in each method that changes the aggregate.</summary>
    protected void Changed() => Version++;

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
