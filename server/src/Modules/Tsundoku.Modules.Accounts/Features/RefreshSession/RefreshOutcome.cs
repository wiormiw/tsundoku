using System.Security.Claims;

namespace Tsundoku.Modules.Accounts.Features.RefreshSession;

/// <summary>
/// Both cases are successes to the unit of work, so the revocation that <see cref="ReuseDetected"/> made is saved;
/// the endpoint still answers it with the same 401 as every other refusal.
/// </summary>
internal closed record RefreshOutcome
{
    internal sealed record Refreshed(ClaimsPrincipal Principal) : RefreshOutcome;

    internal sealed record ReuseDetected : RefreshOutcome;
}
