using Microsoft.AspNetCore.Http.HttpResults;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Common.Http;
using Tsundoku.Common.Messaging;

namespace Tsundoku.Modules.Accounts.Features.SignUp;

internal static class SignUpEndpoint
{
    public static void Map(IEndpointRouteBuilder auth) => auth
        .MapPost("/sign-up", Handle)
        .AllowAnonymous()
        .WithName("SignUp")
        .WithSummary("Sign up. A new and an existing email both get an empty 202; the confirmation or a notice goes by mail.")
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status403Forbidden);

    private static async Task<Results<Accepted, ProblemHttpResult>> Handle(
        SignUpRequest request, ICommandHandler<SignUpCommand, Success> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new SignUpCommand(request.Email, request.Password), cancellationToken)).ToAccepted();
}
