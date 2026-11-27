using Tsundoku.Common.Messaging;

namespace Tsundoku.Modules.Todos.Features.ListTodoLists;

internal sealed record ListTodoListsQuery(int Page, int PageSize, string? Search, bool IncludeTotal) : IQuery<PagedResponse<TodoListSummaryResponse>>;
