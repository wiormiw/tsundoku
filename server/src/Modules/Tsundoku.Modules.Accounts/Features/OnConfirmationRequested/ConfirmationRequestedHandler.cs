using Microsoft.AspNetCore.Identity;
using Tsundoku.Common.Events;
using Tsundoku.Modules.Accounts.Domain;
using Tsundoku.Modules.Accounts.Persistence;

namespace Tsundoku.Modules.Accounts.Features.OnConfirmationRequested;

/// <summary>Sends the confirmation mail after the commit. A re-run sends at most one more mail, and nothing once the user is confirmed.</summary>
internal sealed class ConfirmationRequestedHandler(UserManager<AccountUser> users, ConfirmationMail confirmation)
    : IDomainEventHandler<ConfirmationRequested>
{
    public async Task Handle(ConfirmationRequested domainEvent, CancellationToken cancellationToken)
    {
        if (await users.FindByIdAsync(domainEvent.UserId.ToString()) is { EmailConfirmed: false } user)
            await confirmation.SendAsync(user, cancellationToken);
    }
}
