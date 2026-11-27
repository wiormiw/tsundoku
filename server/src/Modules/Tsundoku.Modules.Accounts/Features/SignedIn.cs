using System.Security.Claims;

namespace Tsundoku.Modules.Accounts.Features;

/// <summary>A started session: the endpoint signs this principal in with the bearer scheme, which writes the tokens.</summary>
internal sealed record SignedIn(ClaimsPrincipal Principal);
