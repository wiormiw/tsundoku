using Microsoft.AspNetCore.Identity;

namespace Tsundoku.Modules.Accounts.Persistence;

/// <summary>The Identity user. Its table has no <c>user_id</c> column (its own id is the user id), so it carries no owner policy.</summary>
internal sealed class AccountUser : IdentityUser<Guid>
{
    public AccountUser() => Id = Guid.CreateVersion7();
}
