using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

namespace Tsundoku.Modules.Accounts.Features;

/// <summary>
/// Checks a Google ID token: signature against Google's JWKS, issuer, audience, lifetime against the injected clock, the claims
/// sign-in needs, and that it was never used before. Null means refused; the reason goes to the log, the token never does.
/// </summary>
internal sealed partial class GoogleIdTokenVerifier(
    IConfigurationManager<OpenIdConnectConfiguration> metadata,
    IConnectionMultiplexer valkey,
    TimeProvider clock,
    IOptions<GoogleOptions> options,
    ILogger<GoogleIdTokenVerifier> logger)
{
    public const string HttpClientName = "Accounts.Google";
    public const string LoginProvider = "Google";

    private static readonly string[] Issuers = ["https://accounts.google.com", "accounts.google.com"];
    private static readonly string[] Algorithms = [SecurityAlgorithms.RsaSha256];
    private static readonly JsonWebTokenHandler Handler = new();

    public async Task<GoogleIdentity?> VerifyAsync(string idToken, CancellationToken cancellationToken)
    {
        var google = options.Value;
        var result = await ValidateAsync(idToken, google, cancellationToken);
        if (result.Exception is SecurityTokenSignatureKeyNotFoundException)
        {
            // Google rotated its keys since the last fetch; the manager rate-limits refreshes, so a forged kid cannot hammer Google.
            metadata.RequestRefresh();
            result = await ValidateAsync(idToken, google, cancellationToken);
        }
        if (!result.IsValid) return Refused(result.Exception?.GetType().Name ?? "invalid");

        var token = (JsonWebToken)result.SecurityToken;
        var now = clock.GetUtcNow();
        if (token.ValidTo == DateTime.MinValue || token.IssuedAt == DateTime.MinValue) return Refused("no_lifetime");
        var expires = new DateTimeOffset(token.ValidTo, TimeSpan.Zero) + google.ClockSkew;
        if (expires <= now) return Refused("expired");
        if (new DateTimeOffset(token.IssuedAt, TimeSpan.Zero) - google.ClockSkew > now) return Refused("issued_in_future");

        if (!token.TryGetPayloadValue<string>(JwtRegisteredClaimNames.Sub, out var subject) || string.IsNullOrEmpty(subject)
            || !token.TryGetPayloadValue<string>(JwtRegisteredClaimNames.Email, out var email) || string.IsNullOrEmpty(email)
            || !EmailVerified(token, out var emailVerified))
            return Refused("claims");

        // Single use: an ID token lives about an hour, so a copy (a log, a proxy) must not sign in again. Valkey down throws: fail closed.
        if (!await valkey.GetDatabase().StringSetAsync(ReplayKey(idToken), 1, expires - now, When.NotExists))
            return Refused("replayed");
        return new GoogleIdentity(subject, email, emailVerified);
    }

    /// <summary>The Valkey key that marks a token used: its hash, so the store never holds a usable token.</summary>
    internal static string ReplayKey(string idToken) =>
        "accounts:google-id-token:" + Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(idToken)));

    private async Task<TokenValidationResult> ValidateAsync(string idToken, GoogleOptions google, CancellationToken cancellationToken)
    {
        var configuration = await metadata.GetConfigurationAsync(cancellationToken);
        return await Handler.ValidateTokenAsync(idToken, new TokenValidationParameters
        {
            ValidIssuers = Issuers,
            ValidAudiences = google.ClientIds,
            IssuerSigningKeys = configuration.SigningKeys,
            ValidAlgorithms = Algorithms,
            // Checked against the injected clock below, not the system clock.
            ValidateLifetime = false,
            RequireExpirationTime = false,
        });
    }

    // Google sends a JSON boolean; older tokens carried the string "true".
    private static bool EmailVerified(JsonWebToken token, out bool verified)
    {
        if (token.TryGetPayloadValue("email_verified", out verified)) return true;
        verified = false;
        return token.TryGetPayloadValue<string>("email_verified", out var text) && bool.TryParse(text, out verified);
    }

    private GoogleIdentity? Refused(string reason)
    {
        Log.Refused(logger, reason);
        return null;
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 2050, Level = LogLevel.Information, Message = "Google ID token refused: {Reason}")]
        public static partial void Refused(ILogger logger, string reason);
    }
}
