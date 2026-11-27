using Tsundoku.Common.Messaging;
using Tsundoku.Modules.Todos.Domain;

namespace Tsundoku.Modules.Todos.Features.AddTodoItem;

internal sealed record AddTodoItemCommand(TodoListId ListId, string Title) : ICommand<IdResponse>;
