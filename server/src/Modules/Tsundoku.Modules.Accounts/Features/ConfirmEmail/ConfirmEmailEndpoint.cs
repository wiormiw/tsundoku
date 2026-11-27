using Microsoft.AspNetCore.Http.HttpResults;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Common.Http;
using Tsundoku.Common.Messaging;

namespace Tsundoku.Modules.Accounts.Features.ConfirmEmail;

internal static class ConfirmEmailEndpoint
{
    public static void Map(IEndpointRouteBuilder auth) => auth
        .MapGet("/confirm-email", Handle)
        .AllowAnonymous()
        .WithName("ConfirmEmail")
        .WithSummary("The link in the confirmation mail. A plain text page, because a browser opens it.")
        .ProducesProblem(StatusCodes.Status400BadRequest);

    private static async Task<Results<ContentHttpResult, ProblemHttpResult>> Handle(
        Guid userId, string code, ICommandHandler<ConfirmEmailCommand, Success> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new ConfirmEmailCommand(userId, code), cancellationToken)) switch
        {
            Success => TypedResults.Text("Your email is confirmed. You can sign in now."),
            Error error => error.ToProblem(),
        };
}
