using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Tsundoku.Common.Valkey;

/// <summary>
/// The readiness check of Valkey: does a PING come back? Tagged "ready", never "live", like the
/// database check: a Valkey that is down must not restart every replica (tip 18.03).
/// </summary>
internal sealed class ValkeyHealthCheck(IConnectionMultiplexer valkey) : IHealthCheck
{
    public const string Name = "valkey";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // PingAsync takes no token; the registration's timeout cancels this wait.
            await valkey.GetDatabase().PingAsync().WaitAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "Valkey does not answer.", exception);
        }
    }
}
