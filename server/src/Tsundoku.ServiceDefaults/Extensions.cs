using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// What all hosts share, based on the Aspire ServiceDefaults: OpenTelemetry for logs, traces and metrics,
/// health checks, and resilient HttpClients with service discovery.
/// It exports over OTLP when OTEL_EXPORTER_OTLP_ENDPOINT is set (the Aspire dashboard and the LGTM stack set it).
/// </summary>
public static class Extensions
{
    public const string LivePath = "/health/live";
    public const string ReadyPath = "/health/ready";

    /// <summary>The trace sources and meters of the app itself (the logging decorator and AppMetrics).</summary>
    private static readonly string[] ApplicationSources = ["Tsundoku.Application"];

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Every outgoing call gets a timeout, retries with jitter and a circuit breaker (tip 15.01).
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });
        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter("Npgsql")
                .AddMeter(ApplicationSources))
            .WithTracing(tracing => tracing
                .AddSource(builder.Environment.ApplicationName)
                .AddSource(ApplicationSources)
                .AddAspNetCoreInstrumentation(aspnet =>
                    // Do not trace health probes. They come every few seconds and hide the real requests.
                    aspnet.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation()
                .AddNpgsql());

        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // "live" says if the process must restart. "ready" says if it can take traffic (tip 18.03).
        builder.Services.AddHealthChecks().AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);
        return builder;
    }

    /// <summary>Liveness checks only the process. Readiness also checks the database. Both are public.</summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks(LivePath, new HealthCheckOptions { Predicate = registration => registration.Tags.Contains("live") })
            .AllowAnonymous().DisableRateLimiting();
        app.MapHealthChecks(ReadyPath, new HealthCheckOptions { Predicate = registration => registration.Tags.Contains("ready") })
            .AllowAnonymous().DisableRateLimiting();
        return app;
    }
}
