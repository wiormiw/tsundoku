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
/// The password is checked again because a bearer token alone must not delete the account (it may be stolen).
/// </summary>
internal sealed partial class DeleteAccountHandler(
    SignInManager<AccountUser> signIn, AccountsDbContext db, ICurrentUser current, CredentialTiming timing, ILogger<DeleteAccountHandler> logger)
    : ICommandHandler<DeleteAccountCommand, DeleteAccountOutcome>
{
    private static readonly DeleteAccountOutcome.Refused Invalid = new(AccountErrors.InvalidCredentials);

    public async Task<Result<DeleteAccountOutcome>> Handle(DeleteAccountCommand command, CancellationToken cancellationToken)
    {
        var users = signIn.UserManager;
        if (await users.FindByIdAsync(current.Id.Value) is not { } user) return AccountErrors.SessionEnded;

        // Task 7 seam: a user without a password proves itself with a fresh Google ID token (command.GoogleIdToken).
        // Until then that case cannot be proven, so it is refused the same way as a missing proof.
        if (user.PasswordHash is null || string.IsNullOrEmpty(command.Password)) return AccountErrors.ReauthenticationRequired;

        await SignInLock.AcquireAsync(db, user, cancellationToken);
        var checkedIn = await signIn.CheckPasswordSignInAsync(user, command.Password, lockoutOnFailure: true);
        if (!checkedIn.Succeeded)
        {
            if (checkedIn.IsLockedOut) timing.VerifyDummy(command.Password);
            Log.Refused(logger, user.Id);
            return Invalid;
        }

        var deleted = await users.DeleteAsync(user);
        if (!deleted.Succeeded) throw new InvalidOperationException($"Deleting user {user.Id} failed: {deleted.Errors.FirstOrDefault()?.Code}");
        Log.AccountDeleted(logger, user.Id);
        return new DeleteAccountOutcome.Deleted();
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 2041, Level = LogLevel.Warning, Message = "Account deletion refused for user {UserId}: wrong password or locked out")]
        public static partial void Refused(ILogger logger, Guid userId);

        [LoggerMessage(EventId = 2042, Level = LogLevel.Information, Message = "AccountDeleted {UserId}")]
        public static partial void AccountDeleted(ILogger logger, Guid userId);
    }
}
