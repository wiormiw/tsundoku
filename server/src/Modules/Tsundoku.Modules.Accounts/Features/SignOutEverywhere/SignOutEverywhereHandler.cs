using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Common.Identity;
using Tsundoku.Common.Messaging;
using Tsundoku.Modules.Accounts.Domain;
using Tsundoku.Modules.Accounts.Persistence;

namespace Tsundoku.Modules.Accounts.Features.SignOutEverywhere;

/// <summary>
/// Every refresh token dies at once (revoked sessions, new security stamp). An access token already issued works until it expires:
/// it is not checked against the database on each request.
/// </summary>
internal sealed partial class SignOutEverywhereHandler(
    AccountsDbContext db, UserManager<AccountUser> users, ICurrentUser current, TimeProvider clock, ILogger<SignOutEverywhereHandler> logger)
    : ICommandHandler<SignOutEverywhereCommand, Success>
{
    public async Task<Result<Success>> Handle(SignOutEverywhereCommand command, CancellationToken cancellationToken)
    {
        var owner = current.Id;
        if (await users.FindByIdAsync(owner.Value) is not { } user) return AccountErrors.SessionEnded;

        var now = clock.GetUtcNow();
        var sessions = await db.Sessions.Where(s => s.UserId == owner && s.RevokedAt == null).ToListAsync(cancellationToken);
        foreach (var session in sessions) session.Revoke(now);
        await users.UpdateSecurityStampAsync(user);
        Log.SignedOutEverywhere(logger, user.Id, sessions.Count);
        return Success.Value;
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 2040, Level = LogLevel.Information, Message = "User {UserId} signed out everywhere, {Sessions} sessions revoked")]
        public static partial void SignedOutEverywhere(ILogger logger, Guid userId, int sessions);
    }
}
