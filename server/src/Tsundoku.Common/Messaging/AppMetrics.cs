using System.Diagnostics.Metrics;

namespace Tsundoku.Common.Messaging;

/// <summary>
/// The metrics of the app itself, beside the metrics of ASP.NET Core, EF Core and the runtime.
/// Dashboards and alert rules use these names. Do not rename them.
/// Do not add user or tenant ids as tags: each value makes a new time series (tip 25.11).
/// </summary>
public sealed class AppMetrics
{
    /// <summary>The meter name (kept from before the modules: dashboards use it). ServiceDefaults exports it, and each module creates its own instruments on the same meter.</summary>
    public const string MeterName = "Tsundoku.Application";

    private readonly Histogram<double> _useCaseDuration;

    public AppMetrics(IMeterFactory meters)
    {
        var meter = meters.Create(MeterName);
        _useCaseDuration = meter.CreateHistogram<double>(
            "app.use_case.duration", "s", "The duration of each use case (command or query), with its outcome.",
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = [0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10] });
    }

    /// <summary>Records one use case. <paramref name="outcome"/> is handled, refused, failed or cancelled.</summary>
    public void UseCaseEnded(string useCase, string outcome, string? errorCode, TimeSpan elapsed) =>
        _useCaseDuration.Record(elapsed.TotalSeconds,
            new("use_case", useCase), new("outcome", outcome), new("error.code", errorCode ?? ""));
}
