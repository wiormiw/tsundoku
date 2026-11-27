using Microsoft.EntityFrameworkCore;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Common.Identity;
using Tsundoku.Common.Messaging;
using Tsundoku.Modules.Todos.Domain;
using Tsundoku.Modules.Todos.Persistence;

namespace Tsundoku.Modules.Todos.Features.GetTodoList;

internal sealed class GetTodoListHandler(TodosDbContext db, ICurrentUser user) : IQueryHandler<GetTodoListQuery, TodoListResponse>
{
    public async Task<Result<TodoListResponse>> Handle(GetTodoListQuery query, CancellationToken cancellationToken)
    {
        // One aggregate: load it without tracking and map it in memory. The owner is part of the lookup (tip 19.05).
        var owner = user.Id;
        var list = await db.TodoLists.AsNoTracking().SingleOrDefaultAsync(l => l.Id == query.Id && l.OwnerId == owner, cancellationToken);
        if (list is null) return TodoListErrors.NotFound(query.Id);

        return new TodoListResponse(
            list.Id.Value, list.Title.Value, list.IsDone, list.Version, list.CreatedAt, list.UpdatedAt,
            [.. list.Items.Select(item => new TodoItemResponse(item.Id.Value, item.Title.Value, item.IsDone, item.CompletedAt))]);
    }
}
