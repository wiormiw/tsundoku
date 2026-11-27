using Microsoft.EntityFrameworkCore;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Common.Identity;
using Tsundoku.Common.Messaging;
using Tsundoku.Modules.Todos.Persistence;

namespace Tsundoku.Modules.Todos.Features.ListTodoLists;

/// <summary>The read side: no tracking, and projections to DTOs (tip 10.03). The owner is part of every query (tip 19.05).</summary>
internal sealed class ListTodoListsHandler(TodosDbContext db, ICurrentUser user)
    : IQueryHandler<ListTodoListsQuery, PagedResponse<TodoListSummaryResponse>>
{
    public async Task<Result<PagedResponse<TodoListSummaryResponse>>> Handle(ListTodoListsQuery query, CancellationToken cancellationToken)
    {
        var owner = user.Id;
        var search = query.Search?.Trim();
        var lists = db.TodoLists.AsNoTracking().Where(list => list.OwnerId == owner);
        if (!string.IsNullOrEmpty(search))
            lists = lists.Where(list => EF.Functions.ILike(list.Title.Value, $"%{Escape(search)}%", "\\"));

        int? total = query.IncludeTotal ? await lists.CountAsync(cancellationToken) : null;
        var items = await lists
            .OrderByDescending(list => list.CreatedAt).ThenBy(list => list.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(list => new TodoListSummaryResponse(
                list.Id.Value, list.Title.Value, list.Items.Count, list.Items.Count(item => item.CompletedAt == null), list.CreatedAt))
            .ToListAsync(cancellationToken);
        return new PagedResponse<TodoListSummaryResponse>(items, query.Page, query.PageSize, total);
    }

    /// <summary>The text is a value, not a pattern: <c>%</c> and <c>_</c> from the user match themselves.</summary>
    private static string Escape(string text) => text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
