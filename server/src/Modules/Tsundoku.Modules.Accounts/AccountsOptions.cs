using System.ComponentModel.DataAnnotations;

namespace Tsundoku.Modules.Accounts;

/// <summary>Sign-up, token lifetimes, password and lockout rules. Bound from "Accounts" and checked at startup.</summary>
internal sealed class AccountsOptions
{
    public const string Section = "Accounts";

    /// <summary>Closed by default. The switch closes Google sign-in too.</summary>
    public bool SignUpEnabled { get; init; }

    /// <summary>The address that clients and the links in emails use to reach the API. Absolute: <see cref="AccountsOptionsValidator"/> checks it.</summary>
    [Required]
    public Uri PublicBaseUrl { get; init; } = null!;

    /// <summary>A signed-out device keeps working until its access token expires, so keep this short.</summary>
    [Range(typeof(TimeSpan), "00:01:00", "01:00:00")]
    public TimeSpan AccessTokenLifetime { get; init; } = TimeSpan.FromMinutes(15);

    [Range(typeof(TimeSpan), "01:00:00", "90.00:00:00")]
    public TimeSpan RefreshTokenLifetime { get; init; } = TimeSpan.FromDays(14);

    /// <summary>How long a confirmation link or code works.</summary>
    [Range(typeof(TimeSpan), "00:10:00", "7.00:00:00")]
    public TimeSpan EmailTokenLifetime { get; init; } = TimeSpan.FromDays(1);

    /// <summary>Length only: no rules about digits or symbols.</summary>
    [Range(8, 128)]
    public int PasswordMinLength { get; init; } = 12;

    [Range(3, 100)]
    public int LockoutMaxFailedAttempts { get; init; } = 10;

    [Range(typeof(TimeSpan), "00:01:00", "1.00:00:00")]
    public TimeSpan LockoutDuration { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>The IANA zone of a new user's settings.</summary>
    [Required]
    public string DefaultTimeZone { get; init; } = "Asia/Jakarta";
}
