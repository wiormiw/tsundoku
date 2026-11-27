namespace Tsundoku.Common.Domain;

/// <summary>
/// The caller broke an invariant. This is a bug in the caller, not bad user input: user input gives an <see cref="Results.Error"/>.
/// The API returns it as 409 Conflict.
/// </summary>
public sealed class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
