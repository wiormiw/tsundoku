namespace Tsundoku.Common.Domain.Results;

/// <summary>The caller is not signed in, or its credential is refused. The message never says which part was wrong.</summary>
public sealed record UnauthorizedError(string Code, string Message) : Error(Code, Message);
