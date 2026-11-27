using Microsoft.EntityFrameworkCore;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Common.Identity;
using Tsundoku.Common.Messaging;
using Tsundoku.Modules.Todos.Domain;
using Tsundoku.Modules.Todos.Persistence;

namespace Tsundoku.Modules.Todos.Features.CompleteTodoItem;

internal sealed class CompleteTodoItemHandler(TodosDbContext db, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<CompleteTodoItemCommand, Success>
{
    public async Task<Result<Success>> Handle(CompleteTodoItemCommand command, CancellationToken cancellationToken)
    {
        var owner = user.Id;
        return await db.TodoLists.SingleOrDefaultAsync(list => list.Id == command.ListId && list.OwnerId == owner, cancellationToken) is { } list
            ? list.CompleteItem(command.ItemId, clock.GetUtcNow())
            : TodoListErrors.NotFound(command.ListId);
    }
}
