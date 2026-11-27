using Tsundoku.Common.Messaging;

namespace Tsundoku.Modules.Todos.Features.CreateTodoList;

internal sealed record CreateTodoListCommand(string Title) : ICommand<IdResponse>;
