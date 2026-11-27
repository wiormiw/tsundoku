using Tsundoku.Common.Messaging;
using Tsundoku.Modules.Todos.Domain;

namespace Tsundoku.Modules.Todos.Features.GetTodoList;

internal sealed record GetTodoListQuery(TodoListId Id) : IQuery<TodoListResponse>;
