namespace Tsundoku.Common.Persistence.Outbox;

/// <summary>
/// One domain event, stored in the same transaction as the change that raised it (tip 17.04).
/// The processor sends it after the commit, so an event is never lost and never sent for a change that rolled back.
/// </summary>
public sealed class OutboxMessage
{
    public const int TypeMaxLength = 200;
    public const int ErrorMaxLength = 2000;

    private OutboxMessage() { }

    public OutboxMessage(string type, string payload, DateTimeOffset occurredAt)
    {
        Id = Guid.CreateVersion7();
        Type = type;
        Payload = payload;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private init; }

    /// <summary>The name of the event type (see DomainEventRegistry.Name).</summary>
    public string Type { get; private init; } = null!;

    /// <summary>The event as JSON.</summary>
    public string Payload { get; private init; } = null!;

    public DateTimeOffset OccurredAt { get; private init; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    public void MarkProcessed(DateTimeOffset at)
    {
        ProcessedAt = at;
        LastError = null;
    }

    /// <summary>After the last allowed attempt the message stays in the table as a dead letter (tip 17.07).</summary>
    public void MarkFailed(string error)
    {
        Attempts++;
        LastError = error.Length > ErrorMaxLength ? error[..ErrorMaxLength] : error;
    }
}
