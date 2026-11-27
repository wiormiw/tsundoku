using System.ComponentModel.DataAnnotations;

namespace Tsundoku.Common.Persistence.Outbox;

/// <summary>The outbox processor. Bound from "Outbox" and checked at startup (tip 8.12).</summary>
public sealed class OutboxOptions
{
    public const string Section = "Outbox";

    /// <summary>The wait between two reads of the table when the last read found nothing.</summary>
    [Range(typeof(TimeSpan), "00:00:00.010", "00:10:00")]
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>The messages that one transaction claims. A smaller batch holds its row locks for less time.</summary>
    [Range(1, 1000)]
    public int BatchSize { get; init; } = 50;

    /// <summary>After this many failed attempts a message stays unsent, as a dead letter.</summary>
    [Range(1, 100)]
    public int MaxAttempts { get; init; } = 5;
}
