using Microsoft.AspNetCore.Identity;
using Tsundoku.Modules.Accounts.Persistence;

namespace Tsundoku.Modules.Accounts.Features;

/// <summary>
/// Makes a branch without a user cost one password hash, like a branch with one, so the response time does not tell whether
/// an address is registered. It uses the DI hasher, the one <see cref="UserManager{TUser}"/> uses.
/// </summary>
internal sealed class CredentialTiming(IPasswordHasher<AccountUser> hasher)
{
    private static readonly AccountUser Nobody = new();
    private static string? s_dummyHash;

    public void HashDummy(string password) => hasher.HashPassword(Nobody, password);

    // A racing first call may hash twice; both results verify the same, so no lock.
    public void VerifyDummy(string password) =>
        hasher.VerifyHashedPassword(Nobody, s_dummyHash ??= hasher.HashPassword(Nobody, "dummy-password-for-timing"), password);
}
