using Tsundoku.Common.Domain.Results;

namespace Tsundoku.Modules.Todos.Domain;

/// <summary>
/// The title of a list or an item: trimmed, 1-200 characters. A value object: equal when the text is equal.
/// The only way to make one is <see cref="Create"/>, so every <see cref="Title"/> in the system is valid.
/// </summary>
internal sealed record Title
{
    public const int MaxLength = 200;

    private Title(string value) => Value = value;

    public string Value { get; }

    public static Result<Title> Create(string? text, string field = "title")
    {
        var trimmed = text?.Trim() ?? "";
        return trimmed.Length is 0 or > MaxLength
            ? ValidationError.For(field, $"Must be 1-{MaxLength} characters.")
            : new Title(trimmed);
    }

    /// <summary>For persistence only: the database holds only values that <see cref="Create"/> accepted.</summary>
    internal static Title FromTrusted(string value) => new(value);

    public override string ToString() => Value;
}
