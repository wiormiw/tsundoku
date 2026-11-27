namespace Tsundoku.Common.Domain.Results;

/// <summary>
/// An expected refusal, with a stable <see cref="Code"/> that clients can check.
/// The hierarchy is <c>closed</c>: the compiler knows all kinds of error, so a <c>switch</c> over them needs no default arm,
/// and a new kind gives a compile error in each place that does not handle it yet.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716:Identifiers should not match keywords",
    Justification = "Error is the usual name in C#. The name conflicts only with a keyword of Visual Basic, which does not use this API.")]
public closed record Error(string Code, string Message);
