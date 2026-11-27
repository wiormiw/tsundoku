namespace Tsundoku.Api.Tests.Integration;

/// <summary>A clock that moves only when a test moves it. Timers stay on the system clock, so background work keeps running.</summary>
public sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private long _ticks = start.UtcTicks;

    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);

    public void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);
}
