using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tsundoku.Common.Events;

namespace Tsundoku.Common.Persistence.Outbox;

/// <summary>
/// Sends the stored domain events to their handlers, in the order they occurred, at least once.
/// Each round claims a batch with <c>FOR UPDATE SKIP LOCKED</c>, so many replicas can run it without sending a message two times
/// at the same time (tip 17.05). A crash after a handler ran but before the commit sends that message again later,
/// so handlers must be idempotent (tip 17.06). There is one processor for each module context, reading that module's outbox table.
/// </summary>
public sealed partial class OutboxProcessor<TDbContext>(
    IServiceScopeFactory scopes,
    IOptions<OutboxOptions> options,
    TimeProvider clock,
    ILogger<OutboxProcessor<TDbContext>> logger) : BackgroundService where TDbContext : DbContext
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        while (!stoppingToken.IsCancellationRequested)
        {
            var claimed = 0;
            try
            {
                claimed = await ProcessBatch(settings, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // A database error must not stop the loop: the next round tries again (tip 17.01).
                BatchFailed(logger, exception);
            }

            if (claimed < settings.BatchSize)
            {
                try
                {
                    await Task.Delay(settings.PollInterval, clock, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    /// <returns>The number of messages that the batch claimed.</returns>
    private async Task<int> ProcessBatch(OutboxOptions settings, CancellationToken cancellationToken)
    {
        // One scope for each batch: a new DbContext, so tracked rows do not pile up (tip 17.01).
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var serializer = scope.ServiceProvider.GetRequiredService<OutboxSerializer>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<DomainEventDispatcher>();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // The table is in the module's schema. The name comes from the model, not from input, so it is safe in the SQL text.
        var entity = db.Model.FindEntityType(typeof(OutboxMessage))!;
        var table = $"\"{entity.GetSchema()}\".\"{entity.GetTableName()}\"";
#pragma warning disable EF1002 // the table name comes from the model, the values are parameters
        var batch = await db.Set<OutboxMessage>()
            .FromSqlRaw($$"""
                SELECT * FROM {{table}}
                WHERE processed_at IS NULL AND attempts < {0}
                ORDER BY occurred_at, id
                LIMIT {1}
                FOR UPDATE SKIP LOCKED
                """, settings.MaxAttempts, settings.BatchSize)
            .ToListAsync(cancellationToken);
#pragma warning restore EF1002

        foreach (var message in batch)
        {
            try
            {
                if (serializer.Deserialize(message.Type, message.Payload) is not { } domainEvent)
                {
                    // An older replica during a rolling deploy can see an event type of the newer version.
                    message.MarkFailed($"This version of the app does not know the event type '{message.Type}'.");
                    continue;
                }
                await dispatcher.Dispatch(domainEvent, cancellationToken);
                message.MarkProcessed(clock.GetUtcNow());
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                message.MarkFailed(exception.Message);
                if (message.Attempts >= settings.MaxAttempts) DeadLettered(logger, exception, message.Id, message.Type, message.Attempts);
                else MessageFailed(logger, exception, message.Id, message.Type, message.Attempts);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return batch.Count;
    }

    [LoggerMessage(EventId = 300, Level = LogLevel.Error, Message = "An outbox batch failed")]
    private static partial void BatchFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 301, Level = LogLevel.Warning, Message = "Outbox message {MessageId} ({Type}) failed, attempt {Attempts}")]
    private static partial void MessageFailed(ILogger logger, Exception exception, Guid messageId, string type, int attempts);

    [LoggerMessage(EventId = 302, Level = LogLevel.Error, Message = "Outbox message {MessageId} ({Type}) is a dead letter after {Attempts} attempts")]
    private static partial void DeadLettered(ILogger logger, Exception exception, Guid messageId, string type, int attempts);
}
