using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Tsundoku.Modules.Accounts.Persistence;

/// <summary>Makes the context for <c>dotnet ef</c>. It builds the model without a database or a host.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AccountsDbContext>
{
    public AccountsDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AccountsDbContext>().UseNpgsql("Host=localhost;Database=appdb").Options);
}
