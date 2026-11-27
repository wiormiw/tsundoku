using System.Net;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http.Timeouts;
using Tsundoku.Api.Http;
using Tsundoku.Api.Identity;
using Tsundoku.Common.Identity;
using Tsundoku.Common.Valkey;

namespace Tsundoku.Api.Hosting;

/// <summary>The HTTP part of the host. Each line names the tip that it follows.</summary>
internal static class ApiHosting
{
    public static WebApplicationBuilder AddApiHosting(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddOptions<ApiLimitsOptions>().BindConfiguration(ApiLimitsOptions.Section).ValidateDataAnnotations().ValidateOnStart();
        var limits = builder.Configuration.GetSection(ApiLimitsOptions.Section).Get<ApiLimitsOptions>() ?? new ApiLimitsOptions();

        // Shutdown: finish requests within 25 s. The container grace period (compose: stop_grace_period) must be longer (tip 18.08).
        services.Configure<HostOptions>(host => host.ShutdownTimeout = TimeSpan.FromSeconds(25));

        // Problems: one JSON shape for every error (tip 26.04).
        services.AddProblemDetails();
        services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
        // A body or query that cannot bind throws, so the exception handler writes it with its "code", in all environments.
        services.Configure<RouteHandlerOptions>(routes => routes.ThrowOnBadRequest = true);

        // JSON: an unknown or repeated property is an error, not a silent default (tip 22.02).
        services.ConfigureHttpJsonOptions(options =>
        {
            var json = options.SerializerOptions;
            json.Converters.Add(new JsonStringEnumConverter());
            json.NumberHandling = JsonNumberHandling.Strict;
            json.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
            json.AllowDuplicateProperties = false;
            json.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

        // Auth: the accounts module registers the bearer scheme (Identity bearer tokens).
        // Every endpoint needs a user, unless it says AllowAnonymous (tip 19.04).
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        // Rate limits: a token bucket in Valkey for each user, or for each client address without a user (tip 15.10).
        builder.AddValkeyRateLimits();

        // Timeouts: a slow request ends and frees its connection (tip 26.01).
        services.AddRequestTimeouts(timeouts => timeouts.DefaultPolicy = new RequestTimeoutPolicy { Timeout = limits.RequestTimeout });

        // Proxies: trust X-Forwarded-For and -Proto only from the known proxies (tip 26.03).
        services.Configure<ForwardedHeadersOptions>(forwarded =>
        {
            forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var proxy in limits.KnownProxies) forwarded.KnownProxies.Add(IPAddress.Parse(proxy));
        });

        services.AddOpenApi();
        return builder;
    }

    /// <summary>The middleware order is important: proxies first, then errors, then auth before the limits that read the user.</summary>
    public static WebApplication UseApiHosting(this WebApplication app)
    {
        app.UseForwardedHeaders();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseValkeyRateLimits();
        app.UseRequestTimeouts();
        return app;
    }
}
