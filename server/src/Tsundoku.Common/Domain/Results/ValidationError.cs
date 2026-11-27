namespace Tsundoku.Common.Domain.Results;

/// <summary>Field names and their messages. The shape is the same as RFC 9457 validation problem details.</summary>
public sealed record ValidationError(IReadOnlyDictionary<string, string[]> Errors, string ErrorCode = "validation")
    : Error(ErrorCode, "One or more validation errors occurred.")
{
    /// <param name="field">The JSON name of the field.</param>
    /// <param name="message">What is wrong with it.</param>
    /// <param name="code">A more specific code than <c>validation</c>, when clients branch on it.</param>
    public static ValidationError For(string field, string message, string code = "validation") =>
        new(new Dictionary<string, string[]> { [field] = [message] }, code);
}
