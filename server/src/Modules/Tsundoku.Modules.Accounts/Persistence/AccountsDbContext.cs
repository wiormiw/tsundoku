using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Tsundoku.Common.Persistence;
using Tsundoku.Common.Persistence.Outbox;
using Tsundoku.Modules.Accounts.Domain;

namespace Tsundoku.Modules.Accounts.Persistence;

/// <summary>
/// The EF Core context of the module: schema "accounts". It holds the Identity tables, the Data Protection keys (which sign the
/// bearer tokens), the sessions and the user settings. Only <c>user_settings</c> is owned (row-level security).
/// </summary>
internal sealed class AccountsDbContext(DbContextOptions<AccountsDbContext> options)
    : IdentityUserContext<AccountUser, Guid>(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<AccountSession> Sessions => Set<AccountSession>();

    public DbSet<UserSettings> Settings => Set<UserSettings>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ModulePersistence.ConfigureModel(modelBuilder, AccountsModule.Schema);

        modelBuilder.Entity<AccountUser>().ToTable("users");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        modelBuilder.Entity<DataProtectionKey>().ToTable("data_protection_keys");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AccountsDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        ModulePersistence.ConfigureConventions(configurationBuilder);
        configurationBuilder.Properties<AccountSessionId>().HaveConversion<AccountsConverters.SessionIdConverter>();
        configurationBuilder.Properties<TimeZoneId>().HaveConversion<AccountsConverters.TimeZoneConverter>().HaveMaxLength(64);
    }
}
