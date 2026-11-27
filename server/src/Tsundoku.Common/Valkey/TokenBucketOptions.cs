using System.ComponentModel.DataAnnotations;

namespace Tsundoku.Common.Valkey;

/// <summary>One token bucket: it holds <see cref="TokenLimit"/> tokens and refills them all over <see cref="ReplenishmentPeriod"/>.</summary>
internal sealed class TokenBucketOptions
{
    [Range(1, 100_000)]
    public int TokenLimit { get; init; } = 60;

    [Range(typeof(TimeSpan), "00:00:01", "1.00:00:00")]
    public TimeSpan ReplenishmentPeriod { get; init; } = TimeSpan.FromMinutes(1);
}
