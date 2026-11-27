using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Common.Messaging;
using Tsundoku.Modules.Accounts.Domain;
using Tsundoku.Modules.Accounts.Persistence;

namespace Tsundoku.Modules.Accounts.Features.SignUp;

/// <summary>
/// A new and an existing address get the same answer and the same work: one password hash and one mail.
/// The unit of work saves; UserManager's own save happens inside its transaction.
/// </summary>
internal sealed partial class SignUpHandler(
    UserManager<AccountUser> users, ConfirmationMail confirmation, IAccountMailer mailer, CredentialTiming timing,
    IOptions<AccountsOptions> options, ILogger<SignUpHandler> logger) : ICommandHandler<SignUpCommand, Success>
{
    public async Task<Result<Success>> Handle(SignUpCommand command, CancellationToken cancellationToken)
    {
        if (!options.Value.SignUpEnabled) return AccountErrors.SignUpClosed;

        if (await users.FindByEmailAsync(command.Email) is { } existing)
        {
            timing.HashDummy(command.Password);
            await mailer.SendAlreadyRegisteredAsync(existing);
            return Success.Value;
        }

        var user = new AccountUser { UserName = command.Email, Email = command.Email };
        var created = await users.CreateAsync(user, command.Password);
        if (!created.Succeeded)
        {
            // shortcut: a sign-up racing another for one address ends here without a mail, upgrade if that matters.
            if (created.Errors.Any(error => error.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
                return Success.Value;
            return ValidationError.For(created.Errors.First().Code.StartsWith("Password", StringComparison.Ordinal) ? "password" : "email", created.Errors.First().Description);
        }

        await confirmation.SendAsync(user, cancellationToken);
        Log.SignedUp(logger, user.Id);
        return Success.Value;
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 2010, Level = LogLevel.Information, Message = "Signed up user {UserId}")]
        public static partial void SignedUp(ILogger logger, Guid userId);
    }
}
