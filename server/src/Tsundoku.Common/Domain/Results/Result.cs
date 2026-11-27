namespace Tsundoku.Common.Domain.Results;

/// <summary>
/// The value of a successful operation, or the reason that it was refused. Every use case and every domain operation
/// that can be refused returns it. A plain <c>switch</c> handles both cases: <c>result switch { T value => …, Error error => … }</c>.
/// </summary>
public readonly union Result<T>(T, Error) where T : notnull;
