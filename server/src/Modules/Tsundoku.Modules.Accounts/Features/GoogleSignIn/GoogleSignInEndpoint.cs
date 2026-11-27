using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Net.Http.Headers;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Common.Http;
using Tsundoku.Common.Messaging;

namespace Tsundoku.Modules.Accounts.Features.GoogleSignIn;

internal static class GoogleSignInEndpoint
{
    public static void Map(IEndpointRouteBuilder auth) => auth
        .MapPost("/google", Handle)
        .AllowAnonymous()
        .WithName("GoogleSignIn")
        .WithSummary("Sign in with a Google ID token that the app got from Google itself. Each token works once.")
        .Produces<AccessTokenResponse>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

    private static async Task<Results<SignInHttpResult, ProblemHttpResult>> Handle(
        GoogleSignInRequest request, HttpContext context, ICommandHandler<GoogleSignInCommand, SignedIn> handler, CancellationToken cancellationToken)
    {
        // Tokens and refusals alike: nothing on the way may keep them.
        context.Response.Headers[HeaderNames.CacheControl] = "no-store";
        var command = new GoogleSignInCommand(
            request.IdToken, context.Connection.RemoteIpAddress?.ToString(), context.Request.Headers.UserAgent is { Count: > 0 } agent ? agent.ToString() : null);
        return await handler.Handle(command, cancellationToken) switch
        {
            SignedIn signedIn => TypedResults.SignIn(signedIn.Principal, authenticationScheme: IdentityConstants.BearerScheme),
            Error error => error.ToProblem(),
        };
    }
}
