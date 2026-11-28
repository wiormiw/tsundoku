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

    /// <summary>Deleting a Google-only account needs an ID token issued at most this long ago: a fresh sign-in, not an old token.</summary>
    [Range(typeof(TimeSpan), "00:00:30", "01:00:00")]
    public TimeSpan ReauthenticationMaxAge { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>How often Google's keys are fetched again on a schedule (the library default; it refuses less than 5 min).</summary>
    [Range(typeof(TimeSpan), "00:05:00", "7.00:00:00")]
    public TimeSpan KeysAutomaticRefreshInterval { get; init; } = TimeSpan.FromHours(12);

    /// <summary>The least time between fetches asked for by an unknown key id, so forged key ids cannot hammer Google (the library default).</summary>
    [Range(typeof(TimeSpan), "00:00:01", "1.00:00:00")]
    public TimeSpan KeysRefreshInterval { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The whole fetch of the discovery document or the keys, retries included (the client's timeout wraps its resilience handler).
    /// At most 30 s: ServiceDefaults' standard resilience handler ends the call at its own 30 s total timeout, so more would do nothing.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:00:30")]
    public TimeSpan HttpTimeout { get; init; } = TimeSpan.FromSeconds(30);
}
