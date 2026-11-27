using Tsundoku.Common.Domain;

namespace Tsundoku.Modules.Accounts.Domain;

/// <summary>The settings of one user. The row is made on first use, so sign-up writes no owned row.</summary>
internal sealed class UserSettings : AggregateRoot<UserId>
{
    private UserSettings() { }

    public TimeZoneId TimeZone { get; private set; } = null!;

    public static UserSettings Create(UserId user, TimeZoneId zone) => new() { Id = user, TimeZone = zone };

    public void ChangeTimeZone(TimeZoneId zone)
    {
        if (zone == TimeZone) return;
        TimeZone = zone;
        Changed();
    }
}
