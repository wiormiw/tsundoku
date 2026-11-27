using Tsundoku.Common.Domain;

namespace Tsundoku.Modules.Todos.Domain;

internal sealed record TodoItemCompleted(Guid ListId, Guid ItemId, DateTimeOffset CompletedAt) : IDomainEvent;
