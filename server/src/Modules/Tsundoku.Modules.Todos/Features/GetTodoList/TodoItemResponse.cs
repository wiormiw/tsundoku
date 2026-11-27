namespace Tsundoku.Modules.Todos.Features.GetTodoList;

internal sealed record TodoItemResponse(Guid Id, string Title, bool IsDone, DateTimeOffset? CompletedAt);
