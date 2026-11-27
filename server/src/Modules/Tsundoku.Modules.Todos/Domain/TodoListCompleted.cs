using Tsundoku.Common.Domain;

namespace Tsundoku.Modules.Todos.Domain;

/// <summary>The last open item of the list is done.</summary>
internal sealed record TodoListCompleted(Guid ListId, string OwnerId, int ItemCount) : IDomainEvent;
