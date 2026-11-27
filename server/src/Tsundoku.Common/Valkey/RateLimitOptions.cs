using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace Tsundoku.Common.Valkey;

/// <summary>The rate limits. Bound from "RateLimits" and checked at startup.</summary>
internal sealed class RateLimitOptions
{
    public const string Section = "RateLimits";

    /// <summary>Sign-up, sign-in and the like, for each client address: slows password guessing from one source.</summary>
    [ValidateObjectMembers]
    public TokenBucketOptions Credentials { get; init; } = new() { TokenLimit = 5, ReplenishmentPeriod = TimeSpan.FromMinutes(1) };

    /// <summary>Every other request of a signed-in user, for each user.</summary>
    [ValidateObjectMembers]
    public TokenBucketOptions User { get; init; } = new() { TokenLimit = 300, ReplenishmentPeriod = TimeSpan.FromMinutes(1) };

    /// <summary>Every other request without a user, for each client address.</summary>
    [ValidateObjectMembers]
    public TokenBucketOptions Anonymous { get; init; } = new() { TokenLimit = 60, ReplenishmentPeriod = TimeSpan.FromMinutes(1) };

    /// <summary>How long a request waits for Valkey before it is let through (the limiter fails open).</summary>
    [Range(typeof(TimeSpan), "00:00:00.010", "00:00:05")]
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <summary>The least time between two warnings that Valkey is unavailable, so an outage does not log for each request.</summary>
    [Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
    public TimeSpan WarningInterval { get; init; } = TimeSpan.FromMinutes(1);
}
