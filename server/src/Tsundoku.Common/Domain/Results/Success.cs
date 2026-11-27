namespace Tsundoku.Common.Domain.Results;

/// <summary>The value of a <see cref="Result{T}"/> for an operation that has nothing to return.</summary>
public sealed record Success
{
    public static readonly Success Value = new();

    private Success() { }
}
