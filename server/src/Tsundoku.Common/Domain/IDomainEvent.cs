namespace Tsundoku.Common.Domain;

/// <summary>
/// A fact about a change in the domain, in the past tense ("TodoListCompleted").
/// Persistence writes it to the outbox in the same transaction as the change. A background job sends it on after the commit.
/// Use only primitive values and ids in events: they are stored as JSON and read again later, maybe by a newer version of the app.
/// </summary>
public interface IDomainEvent;
