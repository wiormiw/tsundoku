namespace Tsundoku.Modules.Todos.Features.GetTodoList;

/// <summary>A list with all its items, in the order they were added.</summary>
internal sealed record TodoListResponse(
    Guid Id, string Title, bool IsDone, int Version, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt, IReadOnlyList<TodoItemResponse> Items);
