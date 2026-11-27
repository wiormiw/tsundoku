namespace Tsundoku.Common.Persistence;

/// <summary>A parallel request changed the same aggregate first (optimistic concurrency, tip 10.23). The API returns 409.</summary>
public sealed class ConcurrencyException(Exception inner) : Exception("A different request changed this item at the same time.", inner);
