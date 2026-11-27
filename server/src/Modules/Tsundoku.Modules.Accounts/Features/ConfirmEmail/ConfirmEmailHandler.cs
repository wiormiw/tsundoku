using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Common.Messaging;
using Tsundoku.Modules.Accounts.Domain;
using Tsundoku.Modules.Accounts.Persistence;

namespace Tsundoku.Modules.Accounts.Features.ConfirmEmail;

internal sealed class ConfirmEmailHandler(UserManager<AccountUser> users) : ICommandHandler<ConfirmEmailCommand, Success>
{
    public async Task<Result<Success>> Handle(ConfirmEmailCommand command, CancellationToken cancellationToken)
    {
        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(command.Code));
        }
        catch (FormatException)
        {
            return AccountErrors.ConfirmationInvalid;
        }

        if (await users.FindByIdAsync(command.UserId.ToString()) is not { } user) return AccountErrors.ConfirmationInvalid;
        if (!(await users.ConfirmEmailAsync(user, token)).Succeeded) return AccountErrors.ConfirmationInvalid;
        // The token embeds the security stamp: a new stamp makes this link single-use.
        await users.UpdateSecurityStampAsync(user);
        return Success.Value;
    }
}
