using Tsundoku.Common.Domain;
using Tsundoku.Common.Domain.Results;

namespace Tsundoku.Modules.Accounts.Domain;

/// <summary>
/// One sign-in of one user on one device. The refresh token carries this id and a generation: each refresh needs the current
/// generation and moves it on, so a token that was used before is a stolen copy.
/// </summary>
internal sealed class AccountSession : AggregateRoot<AccountSessionId>
{
    public const int MaxUserAgentLength = 512;

    private AccountSession() { }

    public UserId UserId { get; private set; }

    public int Generation { get; private set; } = 1;

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public string? IpAddress { get; private set; }

    public string? UserAgent { get; private set; }

    public static AccountSession Start(UserId user, DateTimeOffset expiresAt, string? ipAddress, string? userAgent) => new()
    {
        Id = AccountSessionId.New(),
        UserId = user,
        ExpiresAt = expiresAt,
        IpAddress = ipAddress,
        UserAgent = userAgent is { Length: > MaxUserAgentLength } ? userAgent[..MaxUserAgentLength] : userAgent,
    };

    /// <summary>
    /// Returns the new generation. A stale <paramref name="presentedGeneration"/> also revokes the session (the caller must save it):
    /// the thief and the owner are both signed out.
    /// </summary>
    public Result<int> Rotate(int presentedGeneration, DateTimeOffset now, DateTimeOffset newExpiry)
    {
        if (RevokedAt is not null) return AccountErrors.SessionRevoked;
        if (now >= ExpiresAt) return AccountErrors.SessionExpired;
        if (presentedGeneration != Generation)
        {
            Revoke(now);
            return AccountErrors.RefreshReused;
        }

        Generation++;
        ExpiresAt = newExpiry;
        Changed();
        return Generation;
    }

    /// <summary>Idempotent: a second call changes nothing.</summary>
    public void Revoke(DateTimeOffset now)
    {
        if (RevokedAt is not null) return;
        RevokedAt = now;
        Changed();
    }
}
