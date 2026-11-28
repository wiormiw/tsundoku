using Tsundoku.Common.Persistence;
using Tsundoku.Modules.Accounts.Persistence;

namespace Tsundoku.Modules.Accounts.Features;

/// <summary>
/// One password check per user at a time, held until the commit: parallel guesses each see the count the last one wrote,
/// instead of all reading the same count and losing increments to Identity's concurrency stamp. Sign-in and account deletion share it.
/// </summary>
internal static class SignInLock
{
    public static async Task AcquireAsync(AccountsDbContext db, AccountUser user, CancellationToken cancellationToken)
    {
        await AdvisoryLock.AcquireAsync(db.Database, "accounts.sign_in", user.Id.ToString(), cancellationToken);
        // The user was loaded before the lock, so its failed-attempt count may be stale.
        await db.Entry(user).ReloadAsync(cancellationToken);
    }
}
