using Microsoft.AspNetCore.Http.HttpResults;
using Tsundoku.Common.Http;
using Tsundoku.Common.Messaging;

namespace Tsundoku.Modules.Accounts.Features.UpdateSettings;

internal static class UpdateSettingsEndpoint
{
    public static void Map(IEndpointRouteBuilder me) => me
        .MapPatch("/settings", Handle)
        .WithName("UpdateSettings")
        .WithSummary("Change the settings of the signed-in user")
        .ProducesValidationProblem();

    private static async Task<Results<Ok<SettingsResponse>, ProblemHttpResult>> Handle(
        UpdateSettingsRequest request, ICommandHandler<UpdateSettingsCommand, SettingsResponse> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new UpdateSettingsCommand(request.Timezone), cancellationToken)).ToOk();
}
