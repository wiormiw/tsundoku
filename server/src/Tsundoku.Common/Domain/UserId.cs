
namespace Tsundoku.Common.Domain;

/// <summary>
/// The id of a user at the identity provider (the <c>sub</c> claim). It is a value object: it is never empty and never too long.
/// The value comes from a validated token, so a bad value is a bug, not user input.
/// </summary>
public readonly record struct UserId
{
    public const int MaxLength = 128;

    public UserId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxLength)
            throw new DomainException("user.id_invalid", $"A user id must be 1-{MaxLength} characters.");
        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
