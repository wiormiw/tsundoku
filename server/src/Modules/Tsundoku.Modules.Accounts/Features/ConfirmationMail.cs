using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Tsundoku.Modules.Accounts.Persistence;

namespace Tsundoku.Modules.Accounts.Features;

/// <summary>Sends the confirmation link. Sign-up and resend share it, so the link looks the same in both.</summary>
internal sealed class ConfirmationMail(UserManager<AccountUser> users, IAccountMailer mailer, IOptions<AccountsOptions> options)
{
    public const string Path = "/api/auth/confirm-email";

    public async Task SendAsync(AccountUser user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var link = $"{options.Value.PublicBaseUrl.AbsoluteUri.TrimEnd('/')}{Path}?userId={user.Id}&code={code}";
        await mailer.SendConfirmationLinkAsync(user, user.Email!, link);
    }
}
