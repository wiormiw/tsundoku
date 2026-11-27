using Microsoft.AspNetCore.Http.HttpResults;
using Tsundoku.Common.Http;
using Tsundoku.Common.Messaging;

namespace Tsundoku.Modules.Todos.Features.ListTodoLists;

internal static class ListTodoListsEndpoint
{
    public static void Map(IEndpointRouteBuilder lists) => lists
        .MapGet("/", Handle)
        .WithName("ListTodoLists")
        .WithSummary("List the lists of the current user, newest first")
        .ProducesValidationProblem();

    private static async Task<Results<Ok<PagedResponse<TodoListSummaryResponse>>, ProblemHttpResult>> Handle(
        [AsParameters] ListTodoListsRequest request,
        IQueryHandler<ListTodoListsQuery, PagedResponse<TodoListSummaryResponse>> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new ListTodoListsQuery(request.Page, request.PageSize, request.Search, request.IncludeTotal), cancellationToken)).ToOk();
}
