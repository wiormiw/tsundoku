using Microsoft.AspNetCore.Identity;
using Tsundoku.Common.Events;
using Tsundoku.Modules.Accounts.Domain;
using Tsundoku.Modules.Accounts.Persistence;

namespace Tsundoku.Modules.Accounts.Features.OnAlreadyRegisteredNoticeRequested;

/// <summary>Sends the "already registered" notice after the commit. A re-run sends one more notice, which is harmless.</summary>
internal sealed class AlreadyRegisteredNoticeRequestedHandler(UserManager<AccountUser> users, IAccountMailer mailer)
    : IDomainEventHandler<AlreadyRegisteredNoticeRequested>
{
    public async Task Handle(AlreadyRegisteredNoticeRequested domainEvent, CancellationToken cancellationToken)
    {
        if (await users.FindByIdAsync(domainEvent.UserId.ToString()) is { } user) await mailer.SendAlreadyRegisteredAsync(user);
    }
}
