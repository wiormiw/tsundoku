using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tsundoku.Common.Domain;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Common.Messaging;
using Tsundoku.Modules.Accounts.Domain;
using Tsundoku.Modules.Accounts.Persistence;

namespace Tsundoku.Modules.Accounts.Features.RefreshSession;

/// <summary>
/// A refresh token is single use: it carries the session and its generation, and each refresh moves the generation on.
/// A stale generation is a copy, so the session is revoked and the thief and the owner both sign in again.
/// Every refusal answers <see cref="AccountErrors.InvalidRefresh"/>; the reason goes to the log only.
/// </summary>
internal sealed partial class RefreshSessionHandler(
    SignInManager<AccountUser> signIn,
    IOptionsMonitor<BearerTokenOptions> bearer,
    AccountsDbContext db,
    TimeProvider clock,
    IOptions<AccountsOptions> options,
    ILogger<RefreshSessionHandler> logger) : ICommandHandler<RefreshSessionCommand, RefreshOutcome>
{
    public async Task<Result<RefreshOutcome>> Handle(RefreshSessionCommand command, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var ticket = command.RefreshToken is { Length: > 0 } token ? bearer.Get(IdentityConstants.BearerScheme).RefreshTokenProtector.Unprotect(token) : null;
        if (ticket?.Properties.ExpiresUtc is not { } expires) return Refused("unreadable");
        if (expires <= now) return Refused("expired");

        // Null for a deleted user or a changed security stamp (sign-out everywhere, a new password).
        if (await signIn.ValidateSecurityStampAsync(ticket.Principal) is not { } user) return Refused("stamp");

        if (!Guid.TryParse(ticket.Principal.FindFirst(SessionClaims.Sid)?.Value, out var sid)
            || !int.TryParse(ticket.Principal.FindFirst(SessionClaims.Generation)?.Value, out var generation))
            return Refused("claims");

        var owner = new UserId(user.Id.ToString());
        var session = await db.Sessions.SingleOrDefaultAsync(
            session => session.Id == new AccountSessionId(sid) && session.UserId == owner, cancellationToken);
        if (session is null) return Refused("no_session");

        if (session.Rotate(generation, now, now + options.Value.RefreshTokenLifetime) is Error error)
        {
            if (error != AccountErrors.RefreshReused) return Refused(error.Code);
            Log.ReuseDetected(logger, user.Id, sid);
            return new RefreshOutcome.ReuseDetected();
        }
        return new RefreshOutcome.Refreshed(await SessionPrincipal.CreateAsync(signIn, user, session, cancellationToken));
    }

    private UnauthorizedError Refused(string reason)
    {
        Log.Refused(logger, reason);
        return AccountErrors.InvalidRefresh;
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 2030, Level = LogLevel.Information, Message = "Refresh refused: {Reason}")]
        public static partial void Refused(ILogger logger, string reason);

        [LoggerMessage(EventId = 2031, Level = LogLevel.Warning, Message = "Refresh token reused: session {SessionId} of user {UserId} revoked")]
        public static partial void ReuseDetected(ILogger logger, Guid userId, Guid sessionId);
    }
}
