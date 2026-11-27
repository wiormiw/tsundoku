using Tsundoku.Common.Domain;
using Tsundoku.Common.Domain.Results;

namespace Tsundoku.Modules.Todos.Domain;

/// <summary>
/// The aggregate root of a list of things to do. It belongs to one user and owns its items.
/// Code outside the aggregate never changes an item directly: it asks the list, so every rule is checked in one place.
/// </summary>
internal sealed class TodoList : AggregateRoot<TodoListId>
{
    public const int MaxItems = 500;

    private readonly List<TodoItem> _items = [];

    private TodoList() { }

    public UserId OwnerId { get; private set; }

    public Title Title { get; private set; } = null!;

    public IReadOnlyList<TodoItem> Items => _items;

    public bool IsDone => _items.Count > 0 && _items.TrueForAll(item => item.IsDone);

    public static TodoList Create(UserId owner, Title title) => new() { Id = TodoListId.New(), OwnerId = owner, Title = title };

    public bool IsOwnedBy(UserId user) => OwnerId == user;

    public void Rename(Title title)
    {
        if (title == Title) return;
        Title = title;
        Changed();
    }

    /// <summary>Adds an open item at the end of the list.</summary>
    public Result<TodoItem> AddItem(Title title)
    {
        if (_items.Count >= MaxItems) return TodoListErrors.Full;

        var item = new TodoItem(title);
        _items.Add(item);
        Changed();
        return item;
    }

    /// <summary>
    /// Marks an item as done. When it was the last open item, the list is done too.
    /// It is idempotent: an item that is already done stays as it is, and the result is still a success.
    /// </summary>
    public Result<Success> CompleteItem(TodoItemId itemId, DateTimeOffset at)
    {
        if (_items.Find(item => item.Id == itemId) is not { } item) return TodoListErrors.ItemNotFound(itemId);
        if (item.IsDone) return Success.Value;

        item.Complete(at);
        Changed();
        Raise(new TodoItemCompleted(Id.Value, itemId.Value, at));
        if (IsDone) Raise(new TodoListCompleted(Id.Value, OwnerId.Value, _items.Count));
        return Success.Value;
    }
}
