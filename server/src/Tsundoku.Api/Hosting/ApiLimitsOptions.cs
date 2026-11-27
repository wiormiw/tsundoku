using System.ComponentModel.DataAnnotations;

namespace Tsundoku.Api.Hosting;

/// <summary>The limits of each request. Bound from "ApiLimits" and checked at startup (tip 8.12).</summary>
public sealed class ApiLimitsOptions
{
    public const string Section = "ApiLimits";

    /// <summary>The longest time that one request may run (tip 26.01).</summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:10:00")]
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>The IP addresses of the reverse proxies whose X-Forwarded-* headers the app trusts (tip 26.03).</summary>
    public string[] KnownProxies { get; init; } = [];
}
