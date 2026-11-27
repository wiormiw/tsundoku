using Tsundoku.Common.Identity;
using Tsundoku.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Modules.Todos.Domain;
using Tsundoku.Modules.Todos.Persistence;

namespace Tsundoku.Modules.Todos.Features.DeleteTodoList;

/// <summary>The aggregate loads first, so a rule about deletes (or a "deleted" event) has one place to live.</summary>
internal sealed class DeleteTodoListHandler(TodosDbContext db, ICurrentUser user) : ICommandHandler<DeleteTodoListCommand, Success>
{
    public async Task<Result<Success>> Handle(DeleteTodoListCommand command, CancellationToken cancellationToken)
    {
        var owner = user.Id;
        if (await db.TodoLists.SingleOrDefaultAsync(list => list.Id == command.Id && list.OwnerId == owner, cancellationToken) is not { } list)
            return TodoListErrors.NotFound(command.Id);

        db.TodoLists.Remove(list);
        return Success.Value;
    }
}
