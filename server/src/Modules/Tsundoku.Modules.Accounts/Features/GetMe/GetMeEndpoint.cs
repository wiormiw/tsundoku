using Microsoft.AspNetCore.Http.HttpResults;
using Tsundoku.Common.Http;
using Tsundoku.Common.Messaging;

namespace Tsundoku.Modules.Accounts.Features.GetMe;

internal static class GetMeEndpoint
{
    public static void Map(IEndpointRouteBuilder me) => me
        .MapGet("/", Handle)
        .WithName("GetMe")
        .WithSummary("The signed-in user: email, whether it has a password, and its time zone")
        .ProducesProblem(StatusCodes.Status401Unauthorized);

    private static async Task<Results<Ok<MeResponse>, ProblemHttpResult>> Handle(
        IQueryHandler<GetMeQuery, MeResponse> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new GetMeQuery(), cancellationToken)).ToOk();
}
