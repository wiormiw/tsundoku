using Tsundoku.Common.Identity;
using Tsundoku.Common.Messaging;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Modules.Todos.Domain;
using Tsundoku.Modules.Todos.Persistence;

namespace Tsundoku.Modules.Todos.Features.CreateTodoList;

internal sealed partial class CreateTodoListHandler(TodosDbContext db, ICurrentUser user, ILogger<CreateTodoListHandler> logger)
    : ICommandHandler<CreateTodoListCommand, IdResponse>
{
    public Task<Result<IdResponse>> Handle(CreateTodoListCommand command, CancellationToken cancellationToken) =>
        Task.FromResult<Result<IdResponse>>(Title.Create(command.Title) switch
        {
            Title title => Create(title),
            Error error => error,
        });

    private IdResponse Create(Title title)
    {
        var list = TodoList.Create(user.Id, title);
        db.TodoLists.Add(list);
        Log.Created(logger, list.Id.Value);
        return new IdResponse(list.Id.Value);
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 1000, Level = LogLevel.Information, Message = "Created todo list {ListId}")]
        public static partial void Created(ILogger logger, Guid listId);
    }
}
