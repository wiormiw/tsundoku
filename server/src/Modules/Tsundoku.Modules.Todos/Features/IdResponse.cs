namespace Tsundoku.Modules.Todos.Features;

/// <summary>A command that makes something returns only its id. Read it with the query endpoint (CQRS).</summary>
internal sealed record IdResponse(Guid Id);
