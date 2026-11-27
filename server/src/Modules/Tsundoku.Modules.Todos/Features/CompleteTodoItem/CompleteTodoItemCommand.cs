using Tsundoku.Common.Messaging;
using Tsundoku.Common.Domain.Results;
using Tsundoku.Modules.Todos.Domain;

namespace Tsundoku.Modules.Todos.Features.CompleteTodoItem;

/// <summary>Safe to send two times: the second time nothing changes, and the answer is the same.</summary>
internal sealed record CompleteTodoItemCommand(TodoListId ListId, TodoItemId ItemId) : ICommand<Success>;
