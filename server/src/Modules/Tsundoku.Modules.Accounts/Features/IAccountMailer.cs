using Microsoft.AspNetCore.Identity;
using Tsundoku.Modules.Accounts.Persistence;

namespace Tsundoku.Modules.Accounts.Features;

/// <summary>
/// The mailer port: Identity's sender plus the mail that an existing address gets when someone signs up with it again.
/// Replace the registration with an SMTP or API sender in production.
/// </summary>
internal interface IAccountMailer : IEmailSender<AccountUser>
{
    Task SendAlreadyRegisteredAsync(AccountUser user);
}
