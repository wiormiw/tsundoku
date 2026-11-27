namespace Tsundoku.Common.Domain;

/// <summary>
/// The entry point of an aggregate: the only object of the aggregate that code outside it may hold.
/// One transaction changes one aggregate. Repositories exist only for aggregate roots.
/// </summary>
public interface IAggregateRoot
{
    IReadOnlyList<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
