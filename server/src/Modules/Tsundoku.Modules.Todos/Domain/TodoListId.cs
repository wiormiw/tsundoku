namespace Tsundoku.Modules.Todos.Domain;

/// <summary>
/// The id of a list. It is a separate type, so the compiler stops the use of a different id in its place.
/// Version 7 GUIDs sort by creation time, so new index entries go to the end of the B-tree.
/// </summary>
internal readonly record struct TodoListId(Guid Value)
{
    public static TodoListId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
