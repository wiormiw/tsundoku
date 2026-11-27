namespace Tsundoku.Modules.Todos.Features.ListTodoLists;

/// <summary>The query string of the list endpoint. Constructor defaults make each value optional in the query.</summary>
/// <param name="Page">The page number, from 1.</param>
/// <param name="PageSize">Lists on one page, 1-100.</param>
/// <param name="Search">Only lists whose title contains this text, ignoring case.</param>
/// <param name="IncludeTotal">Also count all matching lists. Set it to false for later pages: the count is a second query.</param>
internal sealed record ListTodoListsRequest(int Page = 1, int PageSize = 20, string? Search = null, bool IncludeTotal = true);
