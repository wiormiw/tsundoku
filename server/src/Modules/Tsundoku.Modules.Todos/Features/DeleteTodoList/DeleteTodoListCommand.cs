using Tsundoku.Common.Messaging;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Modules.Todos.Domain;

namespace Tsundoku.Modules.Todos.Features.DeleteTodoList;

internal sealed record DeleteTodoListCommand(TodoListId Id) : ICommand<Success>;
