using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsundoku.Common.Persistence;
using Tsundoku.Modules.Accounts.Domain;

namespace Tsundoku.Modules.Accounts.Persistence;

/// <summary>The key is the user id, so a user has at most one row. The foreign key to users(id) and the owner policy (uuid) are added in the migration: EF refuses a foreign key from a converted UserId to the Guid key.</summary>
internal sealed class UserSettingsConfiguration : IEntityTypeConfiguration<UserSettings>
{
    public void Configure(EntityTypeBuilder<UserSettings> builder)
    {
        builder.ToTable("user_settings");
        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.Id).HasColumnName(RowLevelSecurity.OwnerColumn).HasConversion<AccountsConverters.UserIdAsUuid>().ValueGeneratedNever();
        builder.Property(settings => settings.Version).IsConcurrencyToken();
        builder.Ignore(settings => settings.DomainEvents);
    }
}
