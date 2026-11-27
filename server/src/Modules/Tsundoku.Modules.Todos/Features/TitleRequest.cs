namespace Tsundoku.Modules.Todos.Features;

/// <summary>The body to create or rename a list, or to add an item. The HTTP contract is separate from the commands, so each can change alone.</summary>
internal sealed record TitleRequest
{
    /// <summary>1-200 characters.</summary>
    public required string Title { get; init; }
}
