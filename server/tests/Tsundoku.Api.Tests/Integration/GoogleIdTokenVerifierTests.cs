using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using Tsundoku.Modules.Accounts;
using Tsundoku.Modules.Accounts.Features;

namespace Tsundoku.Api.Tests.Integration;

/// <summary>The verifier on its own, with a local key, a manual clock and the real Valkey (for the single-use mark).</summary>
public sealed class GoogleIdTokenVerifierTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly TimeSpan Skew = TimeSpan.FromSeconds(30);

    private readonly ManualTimeProvider _clock = new(DateTimeOffset.UtcNow);

    private DateTimeOffset Now => _clock.GetUtcNow();

    private IConnectionMultiplexer Valkey
    {
        get
        {
            Assert.SkipWhen(factory.DockerUnavailableReason is not null, factory.DockerUnavailableReason ?? "");
            return factory.Services.GetRequiredService<IConnectionMultiplexer>();
        }
    }

    private GoogleIdTokenVerifier Verifier => new(
        new StaticConfigurationManager<OpenIdConnectConfiguration>(GoogleTokens.Configuration),
        Valkey,
        _clock,
        Options.Create(new GoogleOptions { ClientIds = [GoogleTokens.ClientId], ClockSkew = Skew }),
        NullLogger<GoogleIdTokenVerifier>.Instance);

    private static string Mint(DateTimeOffset now, string audience = GoogleTokens.ClientId, string issuer = GoogleTokens.Issuer,
        bool? emailVerified = true, TimeSpan? issuedOffset = null, TimeSpan? expiresOffset = null, SecurityKey? key = null,
        string algorithm = SecurityAlgorithms.RsaSha256) =>
        GoogleTokens.Mint("sub-1", "g@example.test", now, audience, issuer, emailVerified, issuedOffset, expiresOffset, key, algorithm);

    [Fact]
    public async Task A_valid_token_gives_the_identity()
    {
        var identity = await Verifier.VerifyAsync(Mint(Now), Ct);

        identity.ShouldNotBeNull();
        (identity.Subject, identity.Email, identity.EmailVerified).ShouldBe(("sub-1", "g@example.test", true));
        // The token carries whole seconds.
        (Now - identity.IssuedAt).ShouldBeInRange(TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task A_wrong_audience_issuer_or_key_or_no_email_verified_is_refused()
    {
        var verifier = Verifier;
        (await verifier.VerifyAsync(Mint(Now, audience: "someone-else"), Ct)).ShouldBeNull();
        (await verifier.VerifyAsync(Mint(Now, issuer: "https://evil.example"), Ct)).ShouldBeNull();
        (await verifier.VerifyAsync(Mint(Now, key: new RsaSecurityKey(RSA.Create(2048)) { KeyId = "test-key" }), Ct)).ShouldBeNull();
        (await verifier.VerifyAsync(Mint(Now, key: new RsaSecurityKey(RSA.Create(2048)) { KeyId = "unknown" }), Ct)).ShouldBeNull();
        (await verifier.VerifyAsync(Mint(Now, emailVerified: null), Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task An_unverified_email_is_reported_as_unverified()
    {
        var identity = await Verifier.VerifyAsync(Mint(Now, emailVerified: false), Ct);

        // The verifier passes it on; sign-in refuses it (GoogleSignInTests).
        identity.ShouldNotBeNull();
        identity.EmailVerified.ShouldBeFalse();
    }

    [Fact]
    public async Task Hs256_with_the_public_key_as_secret_and_alg_none_are_refused()
    {
        // The classic key confusion: the public key is no secret, so an HMAC made with it proves nothing.
        var confused = new SymmetricSecurityKey(GoogleTokens.Key.Rsa.ExportSubjectPublicKeyInfo()) { KeyId = "test-key" };
        var hs256 = Mint(Now, key: confused, algorithm: SecurityAlgorithms.HmacSha256);

        var payload = Mint(Now).Split('.')[1];
        var none = Base64Url.EncodeToString(Encoding.UTF8.GetBytes("""{"alg":"none","typ":"JWT","kid":"test-key"}""")) + "." + payload + ".";

        (await Verifier.VerifyAsync(hs256, Ct)).ShouldBeNull();
        (await Verifier.VerifyAsync(none, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task Issued_at_inside_the_skew_passes_and_beyond_it_is_refused()
    {
        (await Verifier.VerifyAsync(Mint(Now, issuedOffset: TimeSpan.FromSeconds(20)), Ct)).ShouldNotBeNull();
        (await Verifier.VerifyAsync(Mint(Now, issuedOffset: TimeSpan.FromMinutes(2)), Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task Expired_inside_the_skew_passes_and_beyond_it_is_refused()
    {
        var early = TimeSpan.FromHours(-1);
        (await Verifier.VerifyAsync(Mint(Now, issuedOffset: early, expiresOffset: TimeSpan.FromSeconds(-20)), Ct)).ShouldNotBeNull();
        (await Verifier.VerifyAsync(Mint(Now, issuedOffset: early, expiresOffset: TimeSpan.FromMinutes(-2)), Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task The_same_token_works_once()
    {
        var token = Mint(Now);

        (await Verifier.VerifyAsync(token, Ct)).ShouldNotBeNull();
        (await Verifier.VerifyAsync(token, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task The_single_use_mark_expires_with_the_token()
    {
        var expiresIn = TimeSpan.FromMinutes(10);
        var token = Mint(Now, expiresOffset: expiresIn);

        (await Verifier.VerifyAsync(token, Ct)).ShouldNotBeNull();

        var ttl = await Valkey.GetDatabase().KeyTimeToLiveAsync(GoogleIdTokenVerifier.ReplayKey(token));
        ttl.ShouldNotBeNull();
        ttl.Value.ShouldBeGreaterThan(TimeSpan.Zero);
        // The token was minted with whole seconds, so the mark may be up to a second shorter, never longer.
        ttl.Value.ShouldBeLessThanOrEqualTo(expiresIn + Skew);
    }
}
