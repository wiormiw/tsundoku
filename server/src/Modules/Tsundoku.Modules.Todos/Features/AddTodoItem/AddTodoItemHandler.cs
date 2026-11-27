using Microsoft.EntityFrameworkCore;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Common.Identity;
using Tsundoku.Common.Messaging;
using Tsundoku.Modules.Todos.Domain;
using Tsundoku.Modules.Todos.Persistence;

namespace Tsundoku.Modules.Todos.Features.AddTodoItem;

internal sealed class AddTodoItemHandler(TodosDbContext db, ICurrentUser user) : ICommandHandler<AddTodoItemCommand, IdResponse>
{
    public async Task<Result<IdResponse>> Handle(AddTodoItemCommand command, CancellationToken cancellationToken) =>
        Title.Create(command.Title) switch
        {
            Title title => await Add(command.ListId, title, cancellationToken),
            Error error => error,
        };

    private async Task<Result<IdResponse>> Add(TodoListId listId, Title title, CancellationToken cancellationToken)
    {
        var owner = user.Id;
        if (await db.TodoLists.SingleOrDefaultAsync(list => list.Id == listId && list.OwnerId == owner, cancellationToken) is not { } list)
            return TodoListErrors.NotFound(listId);

        return list.AddItem(title) switch
        {
            TodoItem item => new IdResponse(item.Id.Value),
            Error error => error,
        };
    }
}
