using Tsundoku.Common.Domain.Results;

namespace Tsundoku.Modules.Accounts.Domain;

/// <summary>All refusals of the accounts domain. The codes are public API: do not change them.</summary>
internal static class AccountErrors
{
    public const string InvalidRefreshCode = "accounts.invalid_refresh";

    public const string SignUpClosedCode = "accounts.sign_up_closed";
    public const string ConfirmationInvalidCode = "accounts.confirmation_invalid";

    public static ForbiddenError SignUpClosed => new(SignUpClosedCode, "Sign-up is closed.");

    // Unknown user, tampered code and expired code look the same, so the answer does not say which users exist.
    public static ValidationError ConfirmationInvalid =>
        ValidationError.For("code", "The confirmation link is not valid or has expired.", ConfirmationInvalidCode);

    public static ValidationError TimeZoneUnknown => ValidationError.For("timezone", "Must be an IANA time zone id such as Asia/Jakarta.");

    public const string InvalidCredentialsCode = "accounts.invalid_credentials";
    public const string EmailNotConfirmedCode = "accounts.email_not_confirmed";

    // Unknown email, wrong password and lockout look the same, so the answer does not say which users exist.
    public static UnauthorizedError InvalidCredentials => new(InvalidCredentialsCode, "The email or password is wrong.");

    // Only after the right password, so it tells nothing to someone who does not know it.
    public static ForbiddenError EmailNotConfirmed => new(EmailNotConfirmedCode, "Confirm your email before signing in.");

    // The one answer to every refused refresh: a thief probing stolen tokens learns nothing. The reasons below go to logs only.
    public static UnauthorizedError InvalidRefresh => new(InvalidRefreshCode, "The refresh token is not valid. Sign in again.");

    // Reasons of AccountSession.Rotate. Never sent: the refresh use case logs them and answers InvalidRefresh.
    public static UnauthorizedError SessionRevoked => new("accounts.session_revoked", "The session was revoked.");

    public static UnauthorizedError SessionExpired => new("accounts.session_expired", "The session has expired.");

    public static UnauthorizedError RefreshReused => new("accounts.refresh_reused", "The refresh token was already used.");
}
