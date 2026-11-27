using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Common.Http;
using Tsundoku.Common.Messaging;

namespace Tsundoku.Modules.Accounts.Features.DeleteAccount;

internal static class DeleteAccountEndpoint
{
    public static void Map(IEndpointRouteBuilder me) => me
        .MapDelete("/", Handle)
        .RequireRateLimiting(RateLimitPolicies.Credentials)
        .WithName("DeleteAccount")
        .WithSummary("Delete the account. Needs the password again (or, for a Google-only account, a fresh ID token).")
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

    private static async Task<Results<NoContent, ProblemHttpResult>> Handle(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] DeleteAccountRequest? request,
        ICommandHandler<DeleteAccountCommand, DeleteAccountOutcome> handler, CancellationToken cancellationToken) =>
        await handler.Handle(new DeleteAccountCommand(request?.Password, request?.GoogleIdToken), cancellationToken) switch
        {
            DeleteAccountOutcome.Deleted => TypedResults.NoContent(),
            DeleteAccountOutcome.Refused refused => refused.Error.ToProblem(),
            Error error => error.ToProblem(),
        };
}
