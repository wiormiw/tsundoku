using Tsundoku.Common.Messaging;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Modules.Todos.Domain;

namespace Tsundoku.Modules.Todos.Features.RenameTodoList;

internal sealed record RenameTodoListCommand(TodoListId Id, string Title) : ICommand<Success>;
