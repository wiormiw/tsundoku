using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Tsundoku.Common.Valkey;

namespace Tsundoku.Api.Tests.Integration;

public sealed class RateLimitTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly TokenBucketOptions FivePerMinute = new() { TokenLimit = 5, ReplenishmentPeriod = TimeSpan.FromMinutes(1) };

    private ValkeyTokenBucket NewBucket(TimeProvider clock) =>
        new(factory.Services.GetRequiredService<IConnectionMultiplexer>(), Options.Create(new RateLimitOptions()), clock, LoggerFactory.Create(_ => { }).CreateLogger<ValkeyTokenBucket>());

    /// <summary>A server that limits tightly, with its own client address: the Valkey is shared, so the buckets must not be.</summary>
    private HttpClient LimitedClient(IReadOnlyDictionary<string, string> settings, string? user = null)
    {
        Assert.SkipWhen(factory.DockerUnavailableReason is not null, factory.DockerUnavailableReason ?? "");
        var address = new IPAddress(Guid.NewGuid().ToByteArray()[..4]);
        var child = factory.WithWebHostBuilder(host =>
        {
            foreach (var (name, value) in settings) host.UseSetting(name, value);
            host.ConfigureServices(services => services.AddSingleton<IStartupFilter>(new RemoteAddress(address)));
        });
        var client = child.CreateClient();
        if (user is not null) client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, user);
        return client;
    }

    [Fact]
    public async Task The_bucket_allows_the_limit_then_refuses_with_retry_after()
    {
        Assert.SkipWhen(factory.DockerUnavailableReason is not null, factory.DockerUnavailableReason ?? "");
        var bucket = NewBucket(TimeProvider.System);
        var key = $"test:{Guid.NewGuid():N}";

        for (var i = 0; i < 5; i++) (await bucket.TryTakeAsync(key, FivePerMinute)).Allowed.ShouldBeTrue();
        var refused = await bucket.TryTakeAsync(key, FivePerMinute);

        refused.Allowed.ShouldBeFalse();
        refused.RetryAfter.ShouldBeGreaterThan(TimeSpan.Zero);
        refused.RetryAfter.ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(13));
    }

    [Fact]
    public async Task Two_instances_share_one_bucket_on_valkeys_clock()
    {
        Assert.SkipWhen(factory.DockerUnavailableReason is not null, factory.DockerUnavailableReason ?? "");
        var first = NewBucket(new ManualTimeProvider(DateTimeOffset.UtcNow));
        var second = NewBucket(new ManualTimeProvider(DateTimeOffset.UtcNow.AddHours(1)));
        var key = $"test:{Guid.NewGuid():N}";

        var allowed = 0;
        for (var i = 0; i < 6; i++)
            if ((await (i % 2 == 0 ? first : second).TryTakeAsync(key, FivePerMinute)).Allowed) allowed++;

        allowed.ShouldBe(5);
    }

    [Fact]
    public async Task It_fails_open_after_the_timeout()
    {
        await using var unreachable = await ConnectionMultiplexer.ConnectAsync("127.0.0.1:1,abortConnect=false,connectTimeout=100");
        var log = new LevelLog();
        var options = Options.Create(new RateLimitOptions { Timeout = TimeSpan.FromMilliseconds(50) });
        var bucket = new ValkeyTokenBucket(unreachable, options, TimeProvider.System, log);

        var started = TimeProvider.System.GetTimestamp();
        var first = await bucket.TryTakeAsync("test:down", FivePerMinute);
        var second = await bucket.TryTakeAsync("test:down", FivePerMinute);

        first.Allowed.ShouldBeTrue();
        second.Allowed.ShouldBeTrue();
        TimeProvider.System.GetElapsedTime(started).ShouldBeLessThan(TimeSpan.FromMilliseconds(500));
        log.Levels.ShouldBe([LogLevel.Warning]); // The second failure falls inside the warning interval.
    }

    [Fact]
    public async Task The_credentials_bucket_refuses_the_fourth_sign_in_with_retry_after()
    {
        using var client = LimitedClient(new Dictionary<string, string> { ["RateLimits:Credentials:TokenLimit"] = "3" });

        for (var i = 0; i < 3; i++) (await client.PostAsJsonAsync("/api/auth/sign-in", new { email = "a@example.test", password = "x" }, Ct)).StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
        var refused = await client.PostAsJsonAsync("/api/auth/sign-in", new { email = "a@example.test", password = "x" }, Ct);

        refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        refused.Headers.RetryAfter!.Delta!.Value.TotalSeconds.ShouldBeGreaterThanOrEqualTo(1);
        (await refused.Content.ReadFromJsonAsync<Dictionary<string, object>>(Ct))!["code"].ToString().ShouldBe("rate_limited");
    }

    [Fact]
    public async Task Rotating_x_forwarded_for_does_not_open_a_new_bucket()
    {
        using var client = LimitedClient(new Dictionary<string, string> { ["RateLimits:Credentials:TokenLimit"] = "3" });

        HttpResponseMessage? last = null;
        for (var i = 1; i <= 4; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/sign-in") { Content = JsonContent.Create(new { email = "a@example.test", password = "x" }) };
            request.Headers.Add("X-Forwarded-For", $"203.0.113.{i}");
            last = await client.SendAsync(request, Ct);
        }

        last!.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Health_is_never_limited()
    {
        using var client = LimitedClient(new Dictionary<string, string> { ["RateLimits:Anonymous:TokenLimit"] = "2" });

        for (var i = 0; i < 50; i++) (await client.GetAsync("/health/live", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Authenticated_requests_use_the_user_bucket()
    {
        var settings = new Dictionary<string, string> { ["RateLimits:User:TokenLimit"] = "2" };
        var alice = Guid.NewGuid().ToString();
        using var first = LimitedClient(settings, alice);

        (await first.GetAsync("/api/todo-lists", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await first.GetAsync("/api/todo-lists", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await first.GetAsync("/api/todo-lists", Ct)).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        using var second = LimitedClient(settings, Guid.NewGuid().ToString());
        (await second.GetAsync("/api/todo-lists", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>The test server has no remote address; this gives every request one, the way Kestrel would.</summary>
    private sealed class RemoteAddress(IPAddress address) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((HttpContext context, RequestDelegate following) =>
            {
                context.Connection.RemoteIpAddress = address;
                return following(context);
            });
            next(app);
        };
    }

    private sealed class LevelLog : ILogger<ValkeyTokenBucket>
    {
        public List<LogLevel> Levels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Levels.Add(logLevel);
    }
}
