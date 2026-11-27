using Tsundoku.Common.Domain;

namespace Tsundoku.Modules.Todos.Domain;

/// <summary>One thing to do. It is part of the <see cref="TodoList"/> aggregate: only the list makes and changes it.</summary>
internal sealed class TodoItem : Entity<TodoItemId>
{
    private TodoItem() { }

    internal TodoItem(Title title)
    {
        Id = TodoItemId.New();
        Title = title;
    }

    public Title Title { get; private set; } = null!;

    public DateTimeOffset? CompletedAt { get; private set; }

    public bool IsDone => CompletedAt is not null;

    internal void Complete(DateTimeOffset at) => CompletedAt = at;
}
