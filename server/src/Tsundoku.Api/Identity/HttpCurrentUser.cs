using System.Security.Claims;
using Tsundoku.Common.Domain;
using Tsundoku.Common.Identity;

namespace Tsundoku.Api.Identity;

/// <summary>The user of the request, from the validated token. The JWT handler keeps the claim names (MapInboundClaims = false).</summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public const string SubjectClaim = "sub";

    public UserId Id =>
        accessor.HttpContext?.User is { Identity.IsAuthenticated: true } user && user.FindFirstValue(SubjectClaim) is { } subject
            ? new UserId(subject)
            : throw new InvalidOperationException("The request has no authenticated user with a 'sub' claim.");
}
