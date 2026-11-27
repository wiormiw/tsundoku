namespace Tsundoku.Common.Domain.Results;

/// <summary>The item does not exist, or it belongs to a different user. The API does not tell which (tip 19.05).</summary>
public sealed record NotFoundError(string Code, string Message) : Error(Code, Message);
