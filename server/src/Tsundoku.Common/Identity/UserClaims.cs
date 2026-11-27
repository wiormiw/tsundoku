namespace Tsundoku.Common.Identity;

/// <summary>Claim names that the host and the identity module must agree on.</summary>
public static class UserClaims
{
    /// <summary>The user id. Identity is configured to use it as its user id claim, and nothing maps inbound claim names.</summary>
    public const string Subject = "sub";
}
