using Microsoft.AspNetCore.Http.HttpResults;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Common.Http;
using Tsundoku.Common.Messaging;

namespace Tsundoku.Modules.Accounts.Features.ResendConfirmation;

internal static class ResendConfirmationEndpoint
{
    public static void Map(IEndpointRouteBuilder auth) => auth
        .MapPost("/resend-confirmation", Handle)
        .AllowAnonymous()
        .RequireRateLimiting(RateLimitPolicies.Credentials)
        .WithName("ResendConfirmation")
        .WithSummary("Mail the confirmation link again. Always an empty 202, whatever the address.")
        .ProducesValidationProblem();

    private static async Task<Results<Accepted, ProblemHttpResult>> Handle(
        ResendConfirmationRequest request, ICommandHandler<ResendConfirmationCommand, Success> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new ResendConfirmationCommand(request.Email), cancellationToken)).ToAccepted();
}
