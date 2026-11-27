using System.Globalization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Tsundoku.Common.Http;
using Tsundoku.Common.Identity;

namespace Tsundoku.Common.Valkey;

/// <summary>
/// One bucket per request, from the endpoint's metadata. The partitions are client addresses or user ids, never an email.
/// The address is the connection's, after the forwarded-headers middleware trusted only the configured proxies.
/// The ASP.NET <c>RequireRateLimiting</c>/<c>DisableRateLimiting</c> metadata is reused on purpose; the built-in limiter is not running.
/// </summary>
internal sealed class ValkeyRateLimitMiddleware(RequestDelegate next, ValkeyTokenBucket bucket, IOptions<RateLimitOptions> options)
{
    private readonly RateLimitOptions _limits = options.Value;

    public async Task InvokeAsync(HttpContext context)
    {
        var metadata = context.GetEndpoint()?.Metadata;
        if (metadata?.GetMetadata<DisableRateLimitingAttribute>() is not null)
        {
            await next(context);
            return;
        }

        // Only the key string and the script's arguments are allocated per request.
        var address = context.Connection.RemoteIpAddress;
        TokenBucketOptions limit;
        string key;
        if (metadata?.GetMetadata<EnableRateLimitingAttribute>() is { } policy)
        {
            if (policy.PolicyName != RateLimitPolicies.Credentials)
                throw new InvalidOperationException($"Unknown rate limit policy '{policy.PolicyName}'.");
            limit = _limits.Credentials;
            key = $"credentials:ip:{address}";
        }
        else if (context.User.FindFirst(UserClaims.Subject)?.Value is { } subject)
        {
            limit = _limits.User;
            key = $"user:{subject}";
        }
        else
        {
            limit = _limits.Anonymous;
            key = $"ip:{address}";
        }

        var decision = await bucket.TryTakeAsync(key, limit);
        if (decision.Allowed)
        {
            await next(context);
            return;
        }

        var seconds = Math.Max(1, (int)Math.Ceiling(decision.RetryAfter.TotalSeconds));
        context.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        await TypedResults.Problem(
            statusCode: StatusCodes.Status429TooManyRequests,
            title: "Too many requests.",
            extensions: new Dictionary<string, object?> { ["code"] = "rate_limited" }).ExecuteAsync(context);
    }
}
