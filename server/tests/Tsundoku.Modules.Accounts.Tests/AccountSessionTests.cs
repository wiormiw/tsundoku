using Tsundoku.Common.Domain;
using Tsundoku.Modules.Accounts.Domain;

namespace Tsundoku.Modules.Accounts.Tests;

public sealed class AccountSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 28, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Expiry = Now.AddDays(14);

    private static AccountSession NewSession() => AccountSession.Start(new UserId(Guid.NewGuid().ToString()), Expiry, "127.0.0.1", "agent");

    [Fact]
    public void A_new_session_is_generation_one() => NewSession().Generation.ShouldBe(1);

    [Fact]
    public void The_user_agent_is_cut()
    {
        var session = AccountSession.Start(new UserId("u"), Expiry, null, new string('a', 600));
        session.UserAgent!.Length.ShouldBe(AccountSession.MaxUserAgentLength);
    }

    [Fact]
    public void A_rotation_moves_the_generation_and_the_expiry()
    {
        var session = NewSession();
        var later = Expiry.AddDays(1);

        session.Rotate(1, Now, later).Value.ShouldBe(2);

        session.Generation.ShouldBe(2);
        session.ExpiresAt.ShouldBe(later);
        session.Version.ShouldBe(1);
    }

    [Fact]
    public void A_used_generation_is_reuse_and_revokes_the_session()
    {
        var session = NewSession();
        session.Rotate(1, Now, Expiry);

        session.Rotate(1, Now, Expiry).Value.ShouldBe(AccountErrors.RefreshReused);

        session.RevokedAt.ShouldBe(Now);
        session.Version.ShouldBe(2);
    }

    [Fact]
    public void Rotation_at_the_expiry_instant_is_refused_without_skew() =>
        NewSession().Rotate(1, Expiry, Expiry.AddDays(1)).Value.ShouldBe(AccountErrors.SessionExpired);

    [Fact]
    public void A_revoked_session_does_not_rotate()
    {
        var session = NewSession();
        session.Revoke(Now);

        session.Rotate(1, Now, Expiry).Value.ShouldBe(AccountErrors.SessionRevoked);
    }

    [Fact]
    public void Revoking_twice_raises_the_version_once()
    {
        var session = NewSession();
        session.Revoke(Now);
        session.Revoke(Now.AddMinutes(1));

        session.Version.ShouldBe(1);
        session.RevokedAt.ShouldBe(Now);
    }

    [Fact]
    public void The_three_reasons_are_distinct_so_the_logs_can_tell_them_apart() =>
        new[] { AccountErrors.SessionRevoked.Code, AccountErrors.SessionExpired.Code, AccountErrors.RefreshReused.Code, AccountErrors.InvalidRefreshCode }
            .Distinct().Count().ShouldBe(4);
}
