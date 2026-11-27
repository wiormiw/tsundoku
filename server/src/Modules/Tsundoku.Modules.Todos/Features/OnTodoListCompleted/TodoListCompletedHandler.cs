using Tsundoku.Common.Events;
using Tsundoku.Modules.Todos.Domain;

namespace Tsundoku.Modules.Todos.Features.OnTodoListCompleted;

/// <summary>
/// A side effect of a committed change. The outbox runs it after the commit, at least once.
/// Put e-mails, push messages or calls to other services here. Make each of them safe to run two times (tip 17.06).
/// </summary>
internal sealed partial class TodoListCompletedHandler(TodosMetrics metrics, ILogger<TodoListCompletedHandler> logger)
    : IDomainEventHandler<TodoListCompleted>
{
    public Task Handle(TodoListCompleted domainEvent, CancellationToken cancellationToken)
    {
        metrics.TodoListCompleted();
        Log.Completed(logger, domainEvent.ListId, domainEvent.ItemCount);
        return Task.CompletedTask;
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 1100, Level = LogLevel.Information, Message = "Todo list {ListId} is done ({ItemCount} items)")]
        public static partial void Completed(ILogger logger, Guid listId, int itemCount);
    }
}
