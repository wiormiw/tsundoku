using Tsundoku.Common.Domain.Results;

namespace Tsundoku.Modules.Accounts.Features.SignIn;

/// <summary>
/// A refused sign-in is a success to the unit of work, not an <see cref="Error"/> result: Identity's failed-attempt count
/// must commit, or the rollback would undo it and lockout would never come.
/// </summary>
internal closed record SignInOutcome
{
    internal sealed record Succeeded(SignedIn SignedIn) : SignInOutcome;

    internal sealed record Refused(Error Error) : SignInOutcome;
}
