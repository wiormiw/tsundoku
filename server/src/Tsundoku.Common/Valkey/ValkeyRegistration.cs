using Microsoft.AspNetCore.Builder;
using StackExchange.Redis;

namespace Tsundoku.Common.Valkey;

/// <summary>Valkey (the Redis protocol): one shared multiplexer and its readiness check.</summary>
public static class ValkeyRegistration
{
    /// <summary>The name of the connection string. It must be the same as the Valkey resource name in the AppHost.</summary>
    public const string ConnectionStringName = "valkey";

    public static IHostApplicationBuilder AddValkey(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

        builder.Services.AddOptions<ValkeyOptions>().BindConfiguration(ValkeyOptions.Section).ValidateDataAnnotations().ValidateOnStart();

        // One multiplexer for the process: it is thread safe and pipelines every caller. Without
        // AbortOnConnectFail a Valkey that is down at startup is retried in the background, and
        // readiness says so, instead of the app refusing to start.
        builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(connectionString);
            options.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(options);
        });

        var timeout = builder.Configuration.GetSection(ValkeyOptions.Section).Get<ValkeyOptions>()?.HealthCheckTimeout
            ?? new ValkeyOptions().HealthCheckTimeout;
        builder.Services.AddHealthChecks().AddCheck<ValkeyHealthCheck>(ValkeyHealthCheck.Name, tags: ["ready"], timeout: timeout);
        return builder;
    }

    /// <summary>The distributed rate limits. They need <see cref="AddValkey"/> and the <see cref="TimeProvider"/> of Common.</summary>
    public static IHostApplicationBuilder AddValkeyRateLimits(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<RateLimitOptions>().BindConfiguration(RateLimitOptions.Section).ValidateDataAnnotations().ValidateOnStart();
        builder.Services.AddSingleton<ValkeyTokenBucket>();
        return builder;
    }

    /// <summary>After authentication and authorization, which fill in the user that the limit reads.</summary>
    public static IApplicationBuilder UseValkeyRateLimits(this IApplicationBuilder app) => app.UseMiddleware<ValkeyRateLimitMiddleware>();
}
