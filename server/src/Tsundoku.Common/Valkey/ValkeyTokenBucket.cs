using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Tsundoku.Common.Valkey;

/// <summary>
/// A token bucket per key, shared by every API instance. The script runs atomically on Valkey and reads Valkey's own
/// clock (<c>TIME</c>), so instances with different clocks still share one bucket. It fails open: when Valkey errs or
/// is slower than <see cref="RateLimitOptions.Timeout"/> the request passes, because readiness already takes the
/// instance out of rotation and refusing every request would turn a cache outage into an API outage.
/// </summary>
internal sealed partial class ValkeyTokenBucket(
    IConnectionMultiplexer valkey, IOptions<RateLimitOptions> options, TimeProvider clock, ILogger<ValkeyTokenBucket> logger)
{
    // Prepared once: StackExchange.Redis sends EVALSHA and loads the script again on NOSCRIPT (a Valkey restart).
    private static readonly LuaScript Script = LuaScript.Prepare("""
        local capacity = tonumber(@Limit)
        local window_ms = tonumber(@PeriodMs)
        local time = redis.call('TIME')
        local now = tonumber(time[1]) * 1000 + math.floor(tonumber(time[2]) / 1000)
        local rate = capacity / window_ms
        local state = redis.call('HMGET', @Key, 'tokens', 'at')
        local tokens = tonumber(state[1])
        local at = tonumber(state[2])
        if tokens == nil or at == nil then
          tokens = capacity
          at = now
        end
        tokens = math.min(capacity, tokens + math.max(0, now - at) * rate)
        local allowed = 0
        local wait_ms = 0
        if tokens >= 1 then
          tokens = tokens - 1
          allowed = 1
        else
          wait_ms = math.ceil((1 - tokens) / rate)
        end
        redis.call('HSET', @Key, 'tokens', tostring(tokens), 'at', now)
        redis.call('PEXPIRE', @Key, window_ms)
        return { allowed, wait_ms }
        """);

    private static readonly RedisKey Prefix = "ratelimit:"u8.ToArray();

    private readonly RateLimitOptions _options = options.Value;
    private long _lastWarnedTicks;

    public async ValueTask<RateLimitDecision> TryTakeAsync(string key, TokenBucketOptions bucket)
    {
        try
        {
            var arguments = new Arguments(new RedisKey(key).Prepend(Prefix), bucket.TokenLimit, (long)bucket.ReplenishmentPeriod.TotalMilliseconds);
            var reply = (long[]?)await Script.EvaluateAsync(valkey.GetDatabase(), arguments).WaitAsync(_options.Timeout) ?? [];
            return reply is [1, ..] ? new RateLimitDecision(true, TimeSpan.Zero) : new RateLimitDecision(false, TimeSpan.FromMilliseconds(reply is [_, var waitMs] ? waitMs : 1000));
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            WarnOnce(exception);
            return new RateLimitDecision(true, TimeSpan.Zero);
        }
    }

    // No key in the line: it holds a client address.
    private void WarnOnce(Exception exception)
    {
        var now = clock.GetUtcNow().UtcTicks;
        var last = Interlocked.Read(ref _lastWarnedTicks);
        if (last != 0 && now - last < _options.WarningInterval.Ticks) return;
        if (Interlocked.CompareExchange(ref _lastWarnedTicks, now, last) == last) LogUnavailable(exception);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rate limit unavailable; the request is let through.")]
    private partial void LogUnavailable(Exception exception);

    // The names are the script's @parameters.
    private readonly record struct Arguments(RedisKey Key, long Limit, long PeriodMs);
}
