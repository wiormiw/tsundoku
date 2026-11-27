using Microsoft.AspNetCore.Identity;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Common.Messaging;
using Tsundoku.Modules.Accounts.Persistence;

namespace Tsundoku.Modules.Accounts.Features.ResendConfirmation;

/// <summary>Always succeeds: the answer never says whether the address exists or is confirmed. Only an unconfirmed user gets a mail.</summary>
internal sealed class ResendConfirmationHandler(UserManager<AccountUser> users, ConfirmationMail confirmation)
    : ICommandHandler<ResendConfirmationCommand, Success>
{
    public async Task<Result<Success>> Handle(ResendConfirmationCommand command, CancellationToken cancellationToken)
    {
        if (await users.FindByEmailAsync(command.Email) is { EmailConfirmed: false } user)
            await confirmation.SendAsync(user, cancellationToken);
        return Success.Value;
    }
}
