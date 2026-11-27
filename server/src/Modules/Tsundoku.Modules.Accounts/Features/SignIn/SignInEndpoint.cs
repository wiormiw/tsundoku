using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Net.Http.Headers;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Common.Http;
using Tsundoku.Common.Messaging;

namespace Tsundoku.Modules.Accounts.Features.SignIn;

internal static class SignInEndpoint
{
    public static void Map(IEndpointRouteBuilder auth) => auth
        .MapPost("/sign-in", Handle)
        .AllowAnonymous()
        .RequireRateLimiting(RateLimitPolicies.Credentials)
        .WithName("SignIn")
        .WithSummary("Sign in with email and password. Gives an access token and a single-use refresh token.")
        .Produces<AccessTokenResponse>()
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

    private static async Task<Results<SignInHttpResult, ProblemHttpResult>> Handle(
        SignInRequest request, HttpContext context, ICommandHandler<SignInCommand, SignInOutcome> handler, CancellationToken cancellationToken)
    {
        // Tokens and refusals alike: nothing on the way may keep them.
        context.Response.Headers[HeaderNames.CacheControl] = "no-store";
        var command = new SignInCommand(
            request.Email, request.Password, context.Connection.RemoteIpAddress?.ToString(), context.Request.Headers.UserAgent is { Count: > 0 } agent ? agent.ToString() : null);
        return await handler.Handle(command, cancellationToken) switch
        {
            SignInOutcome.Succeeded succeeded => TypedResults.SignIn(succeeded.SignedIn.Principal, authenticationScheme: IdentityConstants.BearerScheme),
            SignInOutcome.Refused refused => refused.Error.ToProblem(),
            Error error => error.ToProblem(),
        };
    }
}
