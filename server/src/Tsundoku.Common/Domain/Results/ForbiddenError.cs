namespace Tsundoku.Common.Domain.Results;

/// <summary>The caller is known but may not do this yet (for example, an unconfirmed email).</summary>
public sealed record ForbiddenError(string Code, string Message) : Error(Code, Message);
