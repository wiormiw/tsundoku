using Tsundoku.Common.Domain.Results;

namespace Tsundoku.Modules.Accounts.Domain;

/// <summary>All refusals of the accounts domain. The codes are public API: do not change them.</summary>
internal static class AccountErrors
{
    public const string InvalidRefreshCode = "accounts.invalid_refresh";

    public static ValidationError TimeZoneUnknown => ValidationError.For("timezone", "Must be an IANA time zone id such as Asia/Jakarta.");

    // One code and three messages: the wire never says which refusal happened, so a thief learns nothing from probing.
    public static UnauthorizedError SessionRevoked => new(InvalidRefreshCode, "The refresh token is no longer valid.");

    public static UnauthorizedError SessionExpired => new(InvalidRefreshCode, "The refresh token has expired.");

    public static UnauthorizedError RefreshReused => new(InvalidRefreshCode, "The refresh token was already used.");
}
