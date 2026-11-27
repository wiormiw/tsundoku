using Microsoft.AspNetCore.Http.HttpResults;
using Tsundoku.Common.Http;
using Tsundoku.Common.Messaging;

namespace Tsundoku.Modules.Todos.Features.CreateTodoList;

internal static class CreateTodoListEndpoint
{
    public static void Map(IEndpointRouteBuilder lists) => lists
        .MapPost("/", Handle)
        .WithName("CreateTodoList")
        .WithSummary("Create an empty list. The body has only the id: read the list with its Location.")
        .ProducesValidationProblem();

    private static async Task<Results<Created<IdResponse>, ProblemHttpResult>> Handle(
        TitleRequest request, ICommandHandler<CreateTodoListCommand, IdResponse> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new CreateTodoListCommand(request.Title), cancellationToken)).ToCreated(created => $"/api/todo-lists/{created.Id}");
}
