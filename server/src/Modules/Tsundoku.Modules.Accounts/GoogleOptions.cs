using System.ComponentModel.DataAnnotations;

namespace Tsundoku.Modules.Accounts;

/// <summary>Google sign-in by ID token. Bound from "Accounts:Google" and checked at startup.</summary>
internal sealed class GoogleOptions
{
    public const string Section = "Accounts:Google";

    /// <summary>The accepted audiences, one OAuth client id per platform. Empty turns Google sign-in off.</summary>
    public IReadOnlyList<string> ClientIds { get; set; } = [];

    /// <summary>Google's discovery document; its JWKS is fetched and cached from here. Absolute https: the module checks it.</summary>
    [Required]
    public Uri MetadataAddress { get; init; } = new("https://accounts.google.com/.well-known/openid-configuration");

    /// <summary>The tolerance on <c>exp</c> and <c>iat</c> for clocks that disagree with Google's.</summary>
    [Range(typeof(TimeSpan), "00:00:00", "00:05:00")]
    public TimeSpan ClockSkew { get; init; } = TimeSpan.FromSeconds(30);
}
