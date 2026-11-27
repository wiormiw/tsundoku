using Microsoft.AspNetCore.Http.HttpResults;
using Tsundoku.Common.Http;
using Tsundoku.Common.Messaging;
using Tsundoku.Modules.Todos.Domain;

namespace Tsundoku.Modules.Todos.Features.AddTodoItem;

internal static class AddTodoItemEndpoint
{
    public static void Map(IEndpointRouteBuilder lists) => lists
        .MapPost("/{id:guid}/items", Handle)
        .WithName("AddTodoItem")
        .WithSummary("Add an open item at the end of a list")
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound);

    private static async Task<Results<Created<IdResponse>, ProblemHttpResult>> Handle(
        Guid id, TitleRequest request, ICommandHandler<AddTodoItemCommand, IdResponse> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new AddTodoItemCommand(new TodoListId(id), request.Title), cancellationToken)).ToCreated(_ => $"/api/todo-lists/{id}");
}
