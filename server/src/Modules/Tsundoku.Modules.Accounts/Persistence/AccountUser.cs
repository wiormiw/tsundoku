using Microsoft.AspNetCore.Identity;
using Tsundoku.Common.Domain;
using Tsundoku.Modules.Accounts.Domain;

namespace Tsundoku.Modules.Accounts.Persistence;

/// <summary>
/// The Identity user. Its table has no <c>user_id</c> column (its own id is the user id), so it carries no owner policy.
/// It is an aggregate root only so that the outbox picks up the mail events it raises.
/// </summary>
internal sealed class AccountUser : IdentityUser<Guid>, IAggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public AccountUser() => Id = Guid.CreateVersion7();

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    public void ClearDomainEvents() => _domainEvents.Clear();

    public void RequestConfirmation() => _domainEvents.Add(new ConfirmationRequested(Id));

    public void RequestAlreadyRegisteredNotice() => _domainEvents.Add(new AlreadyRegisteredNoticeRequested(Id));
}
