using System.Diagnostics;
using Tsundoku.Common.Domain.Results;

namespace Tsundoku.Common.Messaging;

/// <summary>
/// The outermost decorator: one trace span, one log line and one duration measurement for each use case, with its result.
/// A refusal (an <see cref="Error"/> result) is correct behavior of the API, so it is logged at Information level with its code.
/// </summary>
internal static partial class LoggingDecorator
{
    public const string ActivitySourceName = "Tsundoku.Application";
    public const string LoggerCategory = "Tsundoku.Application.UseCases";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    internal sealed class CommandHandler<TCommand, TResult>(ICommandHandler<TCommand, TResult> inner, ILoggerFactory loggers, AppMetrics metrics)
        : ICommandHandler<TCommand, TResult> where TCommand : ICommand<TResult> where TResult : notnull
    {
        public Task<Result<TResult>> Handle(TCommand command, CancellationToken cancellationToken) =>
            Run(typeof(TCommand).Name, () => inner.Handle(command, cancellationToken), loggers, metrics);
    }

    internal sealed class QueryHandler<TQuery, TResult>(IQueryHandler<TQuery, TResult> inner, ILoggerFactory loggers, AppMetrics metrics)
        : IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult> where TResult : notnull
    {
        public Task<Result<TResult>> Handle(TQuery query, CancellationToken cancellationToken) =>
            Run(typeof(TQuery).Name, () => inner.Handle(query, cancellationToken), loggers, metrics);
    }

    private static async Task<Result<TResult>> Run<TResult>(
        string useCase, Func<Task<Result<TResult>>> next, ILoggerFactory loggers, AppMetrics metrics) where TResult : notnull
    {
        var logger = loggers.CreateLogger(LoggerCategory);
        using var activity = ActivitySource.StartActivity(useCase);
        var started = Stopwatch.GetTimestamp();
        try
        {
            var result = await next();
            var elapsed = Stopwatch.GetElapsedTime(started);
            if (result is Error error)
            {
                activity?.SetTag("app.error.code", error.Code);
                Refused(logger, useCase, error.Code, elapsed.TotalMilliseconds);
                metrics.UseCaseEnded(useCase, "refused", error.Code, elapsed);
            }
            else
            {
                Handled(logger, useCase, elapsed.TotalMilliseconds);
                metrics.UseCaseEnded(useCase, "handled", null, elapsed);
            }
            return result;
        }
        catch (OperationCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Unset, "cancelled");
            metrics.UseCaseEnded(useCase, "cancelled", null, Stopwatch.GetElapsedTime(started));
            throw;
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            var elapsed = Stopwatch.GetElapsedTime(started);
            Failed(logger, exception, useCase, elapsed.TotalMilliseconds);
            metrics.UseCaseEnded(useCase, "failed", null, elapsed);
            throw;
        }
    }

    [LoggerMessage(EventId = 100, Level = LogLevel.Debug, Message = "{UseCase} handled in {ElapsedMs:0.0} ms")]
    private static partial void Handled(ILogger logger, string useCase, double elapsedMs);

    [LoggerMessage(EventId = 101, Level = LogLevel.Information, Message = "{UseCase} refused with {ErrorCode} after {ElapsedMs:0.0} ms")]
    private static partial void Refused(ILogger logger, string useCase, string errorCode, double elapsedMs);

    [LoggerMessage(EventId = 102, Level = LogLevel.Error, Message = "{UseCase} failed after {ElapsedMs:0.0} ms")]
    private static partial void Failed(ILogger logger, Exception exception, string useCase, double elapsedMs);
}
