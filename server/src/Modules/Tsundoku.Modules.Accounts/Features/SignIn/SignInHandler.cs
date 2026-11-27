using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Tsundoku.Common.Domain;
using Tsundoku.Common.Messaging;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Modules.Accounts.Domain;
using Tsundoku.Modules.Accounts.Persistence;

namespace Tsundoku.Modules.Accounts.Features.SignIn;

/// <summary>
/// Unknown email, wrong password and lockout give one answer and one password hash each. Lockout is Identity's, in Postgres, so
/// every replica shares it. The unit of work saves the session and the failed-attempt count.
/// </summary>
internal sealed partial class SignInHandler(
    SignInManager<AccountUser> signIn,
    CredentialTiming timing,
    AccountsDbContext db,
    TimeProvider clock,
    IOptions<AccountsOptions> options,
    ILogger<SignInHandler> logger) : ICommandHandler<SignInCommand, SignInOutcome>
{
    private static readonly SignInOutcome.Refused Invalid = new(AccountErrors.InvalidCredentials);

    public async Task<Result<SignInOutcome>> Handle(SignInCommand command, CancellationToken cancellationToken)
    {
        var users = signIn.UserManager;
        if (await users.FindByEmailAsync(command.Email) is not { } user)
        {
            timing.VerifyDummy(command.Password);
            return Invalid;
        }

        await SignInLock.AcquireAsync(db, user, cancellationToken);

        // The failure is counted below, where its result is checked, not inside Identity where it is dropped.
        var checkedIn = await signIn.CheckPasswordSignInAsync(user, command.Password, lockoutOnFailure: false);
        if (checkedIn.IsNotAllowed) return await Unconfirmed(user, command.Password);
        if (checkedIn.IsLockedOut)
        {
            // Identity refuses a locked account before hashing: hash anyway, so the time does not tell.
            timing.VerifyDummy(command.Password);
            Log.LockedOut(logger, user.Id);
            return Invalid;
        }
        if (!checkedIn.Succeeded) return await CountFailure(user);

        var now = clock.GetUtcNow();
        var session = AccountSession.Start(new UserId(user.Id.ToString()), now + options.Value.RefreshTokenLifetime, command.IpAddress, command.UserAgent);
        db.Sessions.Add(session);
        Log.SignedIn(logger, user.Id, session.Id.Value);
        return new SignInOutcome.Succeeded(new SignedIn(await SessionPrincipal.CreateAsync(signIn, user, session, cancellationToken)));
    }

    /// <summary>
    /// Identity checks confirmation before lockout and the password, so it would let an unconfirmed account be guessed without
    /// limit, with 403 as the oracle. Here the lockout and the failure count apply to it too.
    /// </summary>
    private async Task<SignInOutcome> Unconfirmed(AccountUser user, string password)
    {
        var users = signIn.UserManager;
        if (await users.IsLockedOutAsync(user))
        {
            timing.VerifyDummy(password);
            return Invalid;
        }
        if (await users.CheckPasswordAsync(user, password)) return new SignInOutcome.Refused(AccountErrors.EmailNotConfirmed);
        return await CountFailure(user);
    }

    private async Task<SignInOutcome> CountFailure(AccountUser user)
    {
        var counted = await signIn.UserManager.AccessFailedAsync(user);
        if (!counted.Succeeded) Log.FailureNotCounted(logger, user.Id, counted.Errors.FirstOrDefault()?.Code);
        return Invalid;
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 2020, Level = LogLevel.Information, Message = "User {UserId} signed in, session {SessionId}")]
        public static partial void SignedIn(ILogger logger, Guid userId, Guid sessionId);

        [LoggerMessage(EventId = 2021, Level = LogLevel.Warning, Message = "Sign-in refused: user {UserId} is locked out")]
        public static partial void LockedOut(ILogger logger, Guid userId);

        [LoggerMessage(EventId = 2022, Level = LogLevel.Error, Message = "A failed sign-in of user {UserId} was not counted: {ErrorCode}")]
        public static partial void FailureNotCounted(ILogger logger, Guid userId, string? errorCode);
    }
}
