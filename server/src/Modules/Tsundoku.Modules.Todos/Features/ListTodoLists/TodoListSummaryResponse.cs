namespace Tsundoku.Modules.Todos.Features.ListTodoLists;

/// <summary>A list without its items, for list pages.</summary>
internal sealed record TodoListSummaryResponse(Guid Id, string Title, int ItemCount, int OpenCount, DateTimeOffset CreatedAt);
