using Microsoft.AspNetCore.Http.HttpResults;
using Tsundoku.Common.Http;
using Tsundoku.Common.Messaging;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Modules.Todos.Domain;

namespace Tsundoku.Modules.Todos.Features.CompleteTodoItem;

internal static class CompleteTodoItemEndpoint
{
    public static void Map(IEndpointRouteBuilder lists) => lists
        .MapPost("/{id:guid}/items/{itemId:guid}/complete", Handle)
        .WithName("CompleteTodoItem")
        .WithSummary("Mark an item as done. A repeated call changes nothing.")
        .ProducesProblem(StatusCodes.Status404NotFound);

    private static async Task<Results<NoContent, ProblemHttpResult>> Handle(
        Guid id, Guid itemId, ICommandHandler<CompleteTodoItemCommand, Success> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new CompleteTodoItemCommand(new TodoListId(id), new TodoItemId(itemId)), cancellationToken)).ToNoContent();
}
