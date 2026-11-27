using Tsundoku.Common.Domain.Results;

namespace Tsundoku.Modules.Todos.Domain;

/// <summary>All refusals of the todo list aggregate, in one place. The codes are public API: do not change them.</summary>
internal static class TodoListErrors
{
    public static NotFoundError NotFound(TodoListId id) => new("todo_list.not_found", $"No todo list has id '{id}'.");

    public static NotFoundError ItemNotFound(TodoItemId id) => new("todo_item.not_found", $"The list has no item with id '{id}'.");

    public static ValidationError Full => ValidationError.For("title", $"A list holds at most {TodoList.MaxItems} items.");
}
