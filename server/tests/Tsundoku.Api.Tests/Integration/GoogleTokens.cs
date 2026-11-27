using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Tsundoku.Api.Tests.Integration;

/// <summary>A local stand-in for Google: one RSA key that the API trusts in tests, and a minter of ID tokens signed with it.</summary>
internal static class GoogleTokens
{
    public const string ClientId = "test-client";
    public const string Issuer = "https://accounts.google.com";

    public static RsaSecurityKey Key { get; } = new(RSA.Create(2048)) { KeyId = "test-key" };

    /// <summary>The discovery document the API reads instead of Google's.</summary>
    public static OpenIdConnectConfiguration Configuration
    {
        get
        {
            var configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
            configuration.SigningKeys.Add(Key);
            return configuration;
        }
    }

    public static string NewSubject() => Guid.NewGuid().ToString("N");

    /// <summary>A valid token unless an argument says otherwise. A random nonce makes every token unique, as Google's are.</summary>
    public static string Mint(
        string subject, string email, DateTimeOffset now, string audience = ClientId, string issuer = Issuer, bool? emailVerified = true,
        TimeSpan? issuedOffset = null, TimeSpan? expiresOffset = null, SecurityKey? key = null, string algorithm = SecurityAlgorithms.RsaSha256)
    {
        Dictionary<string, object> claims = new() { ["sub"] = subject, ["email"] = email, ["nonce"] = Guid.NewGuid().ToString("N") };
        if (emailVerified is { } verified) claims["email_verified"] = verified;
        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Claims = claims,
            IssuedAt = (now + (issuedOffset ?? TimeSpan.Zero)).UtcDateTime,
            Expires = (now + (expiresOffset ?? TimeSpan.FromHours(1))).UtcDateTime,
            SigningCredentials = new SigningCredentials(key ?? Key, algorithm),
        });
    }
}
