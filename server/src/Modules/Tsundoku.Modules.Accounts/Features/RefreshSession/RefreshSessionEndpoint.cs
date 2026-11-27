using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Net.Http.Headers;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Common.Http;
using Tsundoku.Common.Messaging;
using Tsundoku.Modules.Accounts.Domain;

namespace Tsundoku.Modules.Accounts.Features.RefreshSession;

internal static class RefreshSessionEndpoint
{
    public static void Map(IEndpointRouteBuilder auth) => auth
        .MapPost("/refresh", Handle)
        .AllowAnonymous()
        .WithName("RefreshSession")
        .WithSummary("Trade a refresh token for new tokens. Each refresh token works once; a reused one ends the session.")
        .Produces<AccessTokenResponse>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status409Conflict);

    private static async Task<Results<SignInHttpResult, ProblemHttpResult>> Handle(
        RefreshRequest request, HttpContext context, ICommandHandler<RefreshSessionCommand, RefreshOutcome> handler, CancellationToken cancellationToken)
    {
        context.Response.Headers[HeaderNames.CacheControl] = "no-store";
        return await handler.Handle(new RefreshSessionCommand(request.RefreshToken), cancellationToken) switch
        {
            RefreshOutcome.Refreshed refreshed => TypedResults.SignIn(refreshed.Principal, authenticationScheme: IdentityConstants.BearerScheme),
            RefreshOutcome.ReuseDetected => AccountErrors.InvalidRefresh.ToProblem(),
            Error error => error.ToProblem(),
        };
    }
}
