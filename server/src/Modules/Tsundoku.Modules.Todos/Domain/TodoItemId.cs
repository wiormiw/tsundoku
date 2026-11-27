namespace Tsundoku.Modules.Todos.Domain;

/// <summary>The id of one item. It is unique in the whole system, not only in its list.</summary>
internal readonly record struct TodoItemId(Guid Value)
{
    public static TodoItemId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
