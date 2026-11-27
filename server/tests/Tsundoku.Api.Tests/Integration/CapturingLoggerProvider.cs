using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Tsundoku.Api.Tests.Integration;

/// <summary>Keeps every formatted log message (and exception text) so a test can look for something that must not be there.</summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<string> Messages { get; } = new();

    public ILogger CreateLogger(string categoryName) => new Capture(Messages);

    public void Dispose()
    {
    }

    private sealed class Capture(ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            messages.Enqueue(formatter(state, exception) + exception);
    }
}
