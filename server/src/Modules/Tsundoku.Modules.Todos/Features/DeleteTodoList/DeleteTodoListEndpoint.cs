using Microsoft.AspNetCore.Http.HttpResults;
using Tsundoku.Common.Http;
using Tsundoku.Common.Messaging;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Modules.Todos.Domain;

namespace Tsundoku.Modules.Todos.Features.DeleteTodoList;

internal static class DeleteTodoListEndpoint
{
    public static void Map(IEndpointRouteBuilder lists) => lists
        .MapDelete("/{id:guid}", Handle)
        .WithName("DeleteTodoList")
        .WithSummary("Delete a list and its items")
        .ProducesProblem(StatusCodes.Status404NotFound);

    private static async Task<Results<NoContent, ProblemHttpResult>> Handle(
        Guid id, ICommandHandler<DeleteTodoListCommand, Success> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new DeleteTodoListCommand(new TodoListId(id)), cancellationToken)).ToNoContent();
}
