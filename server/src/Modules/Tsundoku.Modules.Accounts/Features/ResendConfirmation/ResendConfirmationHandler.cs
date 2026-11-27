using Microsoft.AspNetCore.Identity;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Common.Messaging;
using Tsundoku.Modules.Accounts.Persistence;

namespace Tsundoku.Modules.Accounts.Features.ResendConfirmation;

/// <summary>Always succeeds: the answer never says whether the address exists or is confirmed. Only an unconfirmed user gets an outbox row, and the mail goes out after the commit.</summary>
internal sealed class ResendConfirmationHandler(UserManager<AccountUser> users)
    : ICommandHandler<ResendConfirmationCommand, Success>
{
    public async Task<Result<Success>> Handle(ResendConfirmationCommand command, CancellationToken cancellationToken)
    {
        if (await users.FindByEmailAsync(command.Email) is { EmailConfirmed: false } user) user.RequestConfirmation();
        return Success.Value;
    }
}
