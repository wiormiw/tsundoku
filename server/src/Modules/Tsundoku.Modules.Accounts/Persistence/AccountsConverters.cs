using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Tsundoku.Common.Domain;
using Tsundoku.Modules.Accounts.Domain;

namespace Tsundoku.Modules.Accounts.Persistence;

/// <summary>
/// Conversions of this module. Here a <see cref="UserId"/> is a uuid (the Identity user id), where Common's convention makes it text,
/// so the two owned tables set <see cref="UserIdAsUuid"/> on their <c>user_id</c> property.
/// </summary>
internal static class AccountsConverters
{
    internal sealed class UserIdAsUuid() : ValueConverter<UserId, Guid>(id => Guid.Parse(id.Value), value => new UserId(value.ToString()));

    internal sealed class SessionIdConverter() : ValueConverter<AccountSessionId, Guid>(id => id.Value, value => new AccountSessionId(value));

    // The database holds only zones that TimeZoneId.Create accepted.
    internal sealed class TimeZoneConverter() : ValueConverter<TimeZoneId, string>(zone => zone.Value, value => TimeZoneId.FromTrusted(value));
}
