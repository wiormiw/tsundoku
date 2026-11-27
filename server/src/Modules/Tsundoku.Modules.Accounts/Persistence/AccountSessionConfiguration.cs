using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsundoku.Common.Persistence;
using Tsundoku.Modules.Accounts.Domain;

namespace Tsundoku.Modules.Accounts.Persistence;

/// <summary>
/// Sessions are read before a user is known (a refresh has only the ticket), so the table has no owner policy: it is exempt in the catalog test.
/// </summary>
internal sealed class AccountSessionConfiguration : IEntityTypeConfiguration<AccountSession>
{
    public void Configure(EntityTypeBuilder<AccountSession> builder)
    {
        builder.ToTable("sessions");
        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id).ValueGeneratedNever();
        builder.Property(session => session.UserId).HasColumnName(RowLevelSecurity.OwnerColumn).HasConversion<AccountsConverters.UserIdAsUuid>();
        builder.Property(session => session.Version).IsConcurrencyToken();
        builder.Property(session => session.IpAddress).HasMaxLength(64);
        builder.Property(session => session.UserAgent).HasMaxLength(AccountSession.MaxUserAgentLength);
        builder.Ignore(session => session.DomainEvents);

        // The foreign key to users(id) is in the migration: EF refuses one from a converted UserId to the Guid key.
        builder.HasIndex(session => session.UserId);
    }
}
