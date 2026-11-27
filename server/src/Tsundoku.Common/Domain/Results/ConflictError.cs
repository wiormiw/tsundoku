namespace Tsundoku.Common.Domain.Results;

/// <summary>The request conflicts with the current state.</summary>
public sealed record ConflictError(string Code, string Message) : Error(Code, Message);
