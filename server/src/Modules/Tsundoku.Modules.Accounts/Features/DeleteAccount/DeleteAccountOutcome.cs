using Tsundoku.Common.Domain.Results;

namespace Tsundoku.Modules.Accounts.Features.DeleteAccount;

/// <summary>
/// A wrong password is a success to the unit of work, not an <see cref="Error"/> result: the failed-attempt count must commit,
/// or deletion would be a way to guess the password without lockout.
/// </summary>
internal closed record DeleteAccountOutcome
{
    internal sealed record Deleted : DeleteAccountOutcome;

    internal sealed record Refused(Error Error) : DeleteAccountOutcome;
}
