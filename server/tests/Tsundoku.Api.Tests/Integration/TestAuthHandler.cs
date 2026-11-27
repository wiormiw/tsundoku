using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Tsundoku.Api.Tests.Integration;

/// <summary>
/// The tests sign in with a header instead of a JWT: <c>X-Test-User: alice</c> gives a user with <c>sub = alice</c>.
/// Without the header the request is judged by the real bearer scheme, so the same factory serves real tokens; no token means 401.
/// </summary>
internal sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string UserHeader = "X-Test-User";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserHeader, out var user) || string.IsNullOrEmpty(user))
            return await Context.AuthenticateAsync(IdentityConstants.BearerScheme);

        var identity = new ClaimsIdentity([new Claim("sub", user.ToString())], SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
