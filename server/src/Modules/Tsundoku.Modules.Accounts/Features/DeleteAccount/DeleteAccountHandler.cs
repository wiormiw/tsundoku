using Microsoft.AspNetCore.Identity;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Common.Identity;
using Tsundoku.Common.Messaging;
using Tsundoku.Modules.Accounts.Domain;
using Tsundoku.Modules.Accounts.Persistence;

namespace Tsundoku.Modules.Accounts.Features.DeleteAccount;

/// <summary>
/// Deletes the user; the foreign keys cascade its sessions, logins, tokens, claims and settings. Rows of other modules are not
/// touched here: they wait for the AccountDeleted integration event (P3).
/// The password (or, without one, a Google ID token) is checked again because a bearer token alone must not delete the account (it may be stolen).
/// </summary>
internal sealed partial class DeleteAccountHandler(
    SignInManager<AccountUser> signIn, AccountsDbContext db, ICurrentUser current, CredentialTiming timing,
    GoogleIdTokenVerifier google, ILogger<DeleteAccountHandler> logger)
    : ICommandHandler<DeleteAccountCommand, DeleteAccountOutcome>
{
    private static readonly DeleteAccountOutcome.Refused Invalid = new(AccountErrors.InvalidCredentials);

    public async Task<Result<DeleteAccountOutcome>> Handle(DeleteAccountCommand command, CancellationToken cancellationToken)
    {
        var users = signIn.UserManager;
        if (await users.FindByIdAsync(current.Id.Value) is not { } user) return AccountErrors.SessionEnded;

        if (user.PasswordHash is null)
        {
            // No password to ask for: a fresh, single-use Google ID token of this account's own Google login is the proof.
            if (string.IsNullOrEmpty(command.GoogleIdToken)) return AccountErrors.ReauthenticationRequired;
            var identity = await google.VerifyAsync(command.GoogleIdToken, cancellationToken);
            var logins = await users.GetLoginsAsync(user);
            if (identity is null || !logins.Any(login => login.LoginProvider == GoogleIdTokenVerifier.LoginProvider && login.ProviderKey == identity.Subject))
            {
                Log.Refused(logger, user.Id);
                return Invalid;
            }
        }
        else
        {
            if (string.IsNullOrEmpty(command.Password)) return AccountErrors.ReauthenticationRequired;
            await SignInLock.AcquireAsync(db, user, cancellationToken);
            var checkedIn = await signIn.CheckPasswordSignInAsync(user, command.Password, lockoutOnFailure: true);
            if (!checkedIn.Succeeded)
            {
                if (checkedIn.IsLockedOut) timing.VerifyDummy(command.Password);
                Log.Refused(logger, user.Id);
                return Invalid;
            }
        }

        var deleted = await users.DeleteAsync(user);
        if (!deleted.Succeeded) throw new InvalidOperationException($"Deleting user {user.Id} failed: {deleted.Errors.FirstOrDefault()?.Code}");
        Log.AccountDeleted(logger, user.Id);
        return new DeleteAccountOutcome.Deleted();
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 2041, Level = LogLevel.Warning, Message = "Account deletion refused for user {UserId}: wrong password, locked out or no matching Google token")]
        public static partial void Refused(ILogger logger, Guid userId);

        [LoggerMessage(EventId = 2042, Level = LogLevel.Information, Message = "AccountDeleted {UserId}")]
        public static partial void AccountDeleted(ILogger logger, Guid userId);
    }
}
