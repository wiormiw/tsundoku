using Microsoft.AspNetCore.Http.HttpResults;
using Tsundoku.Common.Http;
using Tsundoku.Common.Messaging;
using Tsundoku.Modules.Todos.Domain;

namespace Tsundoku.Modules.Todos.Features.GetTodoList;

internal static class GetTodoListEndpoint
{
    public static void Map(IEndpointRouteBuilder lists) => lists
        .MapGet("/{id:guid}", Handle)
        .WithName("GetTodoList")
        .WithSummary("Get a list with its items")
        .ProducesProblem(StatusCodes.Status404NotFound);

    private static async Task<Results<Ok<TodoListResponse>, ProblemHttpResult>> Handle(
        Guid id, IQueryHandler<GetTodoListQuery, TodoListResponse> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new GetTodoListQuery(new TodoListId(id)), cancellationToken)).ToOk();
}
