using Microsoft.AspNetCore.Http.HttpResults;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Common.Http;
using Tsundoku.Common.Messaging;

namespace Tsundoku.Modules.Accounts.Features.SignOutEverywhere;

internal static class SignOutEverywhereEndpoint
{
    public static void Map(IEndpointRouteBuilder me) => me
        .MapDelete("/sessions", Handle)
        .WithName("SignOutEverywhere")
        .WithSummary("Revoke every session of the signed-in user. Refresh tokens stop working now; access tokens at their expiry.")
        .ProducesProblem(StatusCodes.Status401Unauthorized);

    private static async Task<Results<NoContent, ProblemHttpResult>> Handle(
        ICommandHandler<SignOutEverywhereCommand, Success> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new SignOutEverywhereCommand(), cancellationToken)).ToNoContent();
}
