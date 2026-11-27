using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Tsundoku.Common.Domain;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Common.Messaging;
using Tsundoku.Modules.Accounts.Domain;
using Tsundoku.Modules.Accounts.Persistence;

namespace Tsundoku.Modules.Accounts.Features.GoogleSignIn;

/// <summary>
/// A known Google login signs in. Otherwise a confirmed account with the same, Google-verified email is linked, and a new address
/// gets a confirmed account without a password, if sign-up is open. An unconfirmed account is never linked. The unit of work saves;
/// UserManager's own saves happen inside its transaction.
/// </summary>
internal sealed partial class GoogleSignInHandler(
    SignInManager<AccountUser> signIn,
    GoogleIdTokenVerifier google,
    AccountsDbContext db,
    TimeProvider clock,
    IOptions<AccountsOptions> options,
    IOptions<GoogleOptions> googleOptions,
    ILogger<GoogleSignInHandler> logger) : ICommandHandler<GoogleSignInCommand, SignedIn>
{
    public async Task<Result<SignedIn>> Handle(GoogleSignInCommand command, CancellationToken cancellationToken)
    {
        if (googleOptions.Value.ClientIds.Count == 0) return AccountErrors.GoogleNotConfigured;
        if (string.IsNullOrEmpty(command.IdToken) || await google.VerifyAsync(command.IdToken, cancellationToken) is not { } identity)
            return AccountErrors.GoogleTokenInvalid;

        // Two first sign-ins of one Google account at once would both create it; the second waits and finds the login.
        var lockKey = $"accounts.google:{identity.Subject}";
        await db.Database.ExecuteSqlAsync($"select pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", cancellationToken);

        var users = signIn.UserManager;
        var user = await users.FindByLoginAsync(GoogleIdTokenVerifier.LoginProvider, identity.Subject);
        if (user is null)
        {
            var login = new UserLoginInfo(GoogleIdTokenVerifier.LoginProvider, identity.Subject, GoogleIdTokenVerifier.LoginProvider);
            if (await users.FindByEmailAsync(identity.Email) is { } existing)
            {
                if (!existing.EmailConfirmed) return AccountErrors.EmailUnconfirmed;
                if (!identity.EmailVerified) return AccountErrors.GoogleTokenInvalid;
                // One Google account per user: a recycled Google address (a new sub) must not take over the account.
                if ((await users.GetLoginsAsync(existing)).Any(linked => linked.LoginProvider == GoogleIdTokenVerifier.LoginProvider))
                {
                    Log.SecondGoogleLoginRefused(logger, existing.Id);
                    return AccountErrors.GoogleTokenInvalid;
                }
                Ensure(await users.AddLoginAsync(existing, login), "linking");
                Log.Linked(logger, existing.Id);
                user = existing;
            }
            else
            {
                // The sign-up switch closes Google too, or Google would be a way around it.
                if (!options.Value.SignUpEnabled) return AccountErrors.SignUpClosed;
                if (!identity.EmailVerified) return AccountErrors.GoogleTokenInvalid;
                user = new AccountUser { UserName = identity.Email, Email = identity.Email, EmailConfirmed = true };
                if (await CreateAsync(users, user, cancellationToken) is { } conflict) return conflict;
                Ensure(await users.AddLoginAsync(user, login), "linking");
                Log.SignedUp(logger, user.Id);
            }
        }

        var session = AccountSession.Start(
            new UserId(user.Id.ToString()), clock.GetUtcNow() + options.Value.RefreshTokenLifetime, command.IpAddress, command.UserAgent);
        db.Sessions.Add(session);
        Log.SignedIn(logger, user.Id, session.Id.Value);
        return new SignedIn(await SessionPrincipal.CreateAsync(signIn, user, session, cancellationToken));
    }

    /// <summary>
    /// A password sign-up for the same address may commit between the lookup above and this insert: Identity's validator then sees
    /// it, or the unique user name index refuses the insert (23505). Both are a conflict to retry, not a 500.
    /// </summary>
    private static async Task<ConflictError?> CreateAsync(UserManager<AccountUser> users, AccountUser user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IdentityResult created;
        try
        {
            created = await users.CreateAsync(user);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return AccountErrors.SignUpConflict;
        }
        if (created.Errors.Any(error => error.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
            return AccountErrors.SignUpConflict;
        Ensure(created, "creating");
        return null;
    }

    // A failure here is a race with a password sign-up for the same address or a broken store: a 500 and a rollback.
    private static void Ensure(IdentityResult result, string step)
    {
        if (!result.Succeeded) throw new InvalidOperationException($"Google sign-in failed while {step}: {result.Errors.FirstOrDefault()?.Code}");
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 2051, Level = LogLevel.Information, Message = "User {UserId} signed in with Google, session {SessionId}")]
        public static partial void SignedIn(ILogger logger, Guid userId, Guid sessionId);

        [LoggerMessage(EventId = 2052, Level = LogLevel.Information, Message = "Google login linked to user {UserId}")]
        public static partial void Linked(ILogger logger, Guid userId);

        [LoggerMessage(EventId = 2054, Level = LogLevel.Warning, Message = "Google sign-in refused: user {UserId} already has another Google login")]
        public static partial void SecondGoogleLoginRefused(ILogger logger, Guid userId);

        [LoggerMessage(EventId = 2053, Level = LogLevel.Information, Message = "Signed up user {UserId} with Google")]
        public static partial void SignedUp(ILogger logger, Guid userId);
    }
}
