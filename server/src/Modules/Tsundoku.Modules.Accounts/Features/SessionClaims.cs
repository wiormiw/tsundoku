namespace Tsundoku.Modules.Accounts.Features;

/// <summary>Claims of the refresh ticket (the bearer token's protected payload).</summary>
internal static class SessionClaims
{
    public const string Sid = "sid";
    public const string Generation = "gen";
}
