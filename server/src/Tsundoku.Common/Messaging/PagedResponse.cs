namespace Tsundoku.Common.Messaging;

/// <summary>One page of a list.</summary>
/// <param name="Items">The items of this page.</param>
/// <param name="Page">The page number, from 1.</param>
/// <param name="PageSize">The largest number of items on one page.</param>
/// <param name="TotalCount">The number of items on all pages. It is null when the request did not ask for it, because the count is a second query (tip 10.22).</param>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int? TotalCount)
{
    public int? TotalPages => TotalCount is { } total && PageSize > 0 ? (total + PageSize - 1) / PageSize : null;
}
