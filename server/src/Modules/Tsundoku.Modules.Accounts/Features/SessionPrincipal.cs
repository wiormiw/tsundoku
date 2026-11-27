using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Tsundoku.Modules.Accounts.Domain;
using Tsundoku.Modules.Accounts.Persistence;

namespace Tsundoku.Modules.Accounts.Features;

/// <summary>
/// The principal that the bearer handler turns into tokens: Identity's claims (with the security stamp, which refresh checks)
/// plus the session and its generation, which the refresh ticket carries back.
/// </summary>
internal static class SessionPrincipal
{
    public static async Task<ClaimsPrincipal> CreateAsync(
        SignInManager<AccountUser> signIn, AccountUser user, AccountSession session, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var principal = await signIn.CreateUserPrincipalAsync(user);
        ((ClaimsIdentity)principal.Identity!).AddClaims(
        [
            new Claim(SessionClaims.Sid, session.Id.ToString()),
            new Claim(SessionClaims.Generation, session.Generation.ToString(CultureInfo.InvariantCulture)),
        ]);
        return principal;
    }
}
