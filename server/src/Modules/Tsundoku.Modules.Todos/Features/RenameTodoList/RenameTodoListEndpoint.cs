using Microsoft.AspNetCore.Http.HttpResults;
using Tsundoku.Common.Http;
using Tsundoku.Common.Messaging;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Modules.Todos.Domain;

namespace Tsundoku.Modules.Todos.Features.RenameTodoList;

internal static class RenameTodoListEndpoint
{
    public static void Map(IEndpointRouteBuilder lists) => lists
        .MapPut("/{id:guid}", Handle)
        .WithName("RenameTodoList")
        .WithSummary("Change the title of a list")
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

    private static async Task<Results<NoContent, ProblemHttpResult>> Handle(
        Guid id, TitleRequest request, ICommandHandler<RenameTodoListCommand, Success> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new RenameTodoListCommand(new TodoListId(id), request.Title), cancellationToken)).ToNoContent();
}
