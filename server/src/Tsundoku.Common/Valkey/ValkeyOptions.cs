using System.ComponentModel.DataAnnotations;

namespace Tsundoku.Common.Valkey;

/// <summary>The Valkey connection. Bound from "Valkey" and checked at startup (tip 8.12).</summary>
internal sealed class ValkeyOptions
{
    public const string Section = "Valkey";

    /// <summary>How long the readiness check waits for a PING before it reports Valkey as down.</summary>
    [Range(typeof(TimeSpan), "00:00:00.050", "00:00:30")]
    public TimeSpan HealthCheckTimeout { get; init; } = TimeSpan.FromSeconds(1);
}
