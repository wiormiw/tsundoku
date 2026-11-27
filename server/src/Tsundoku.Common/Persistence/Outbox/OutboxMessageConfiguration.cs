using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Tsundoku.Common.Persistence.Outbox;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).ValueGeneratedNever();
        builder.Property(message => message.Type).HasMaxLength(OutboxMessage.TypeMaxLength);
        builder.Property(message => message.Payload).HasColumnType("jsonb");
        builder.Property(message => message.LastError).HasMaxLength(OutboxMessage.ErrorMaxLength);
        // The processor reads only unsent rows. A partial index stays small, however many rows were sent.
        builder.HasIndex(message => message.OccurredAt).HasFilter("processed_at IS NULL");
    }
}
