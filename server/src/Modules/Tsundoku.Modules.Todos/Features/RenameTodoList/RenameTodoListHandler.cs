using Tsundoku.Common.Identity;
using Tsundoku.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Modules.Todos.Domain;
using Tsundoku.Modules.Todos.Persistence;

namespace Tsundoku.Modules.Todos.Features.RenameTodoList;

internal sealed class RenameTodoListHandler(TodosDbContext db, ICurrentUser user) : ICommandHandler<RenameTodoListCommand, Success>
{
    public async Task<Result<Success>> Handle(RenameTodoListCommand command, CancellationToken cancellationToken) =>
        Title.Create(command.Title) switch
        {
            Title title => await Rename(command.Id, title, cancellationToken),
            Error error => error,
        };

    private async Task<Result<Success>> Rename(TodoListId id, Title title, CancellationToken cancellationToken)
    {
        var owner = user.Id;
        if (await db.TodoLists.SingleOrDefaultAsync(list => list.Id == id && list.OwnerId == owner, cancellationToken) is not { } list)
            return TodoListErrors.NotFound(id);

        list.Rename(title);
        return Success.Value;
    }
}
