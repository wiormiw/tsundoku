using Tsundoku.Common.Domain;

namespace Tsundoku.Common.Events;

/// <summary>
/// The event types and their handlers, filled one line at a time by <see cref="DomainEventRegistration"/>. There is no assembly scan.
/// The outbox stores each event under its <see cref="Name"/>, so a renamed class still reads its old messages.
/// </summary>
public sealed class DomainEventRegistry
{
    private readonly Dictionary<string, Type> _types = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, List<Func<IServiceProvider, IDomainEvent, CancellationToken, Task>>> _handlers = [];
    private readonly HashSet<(Type Event, Type Handler)> _registered = [];

    /// <summary>The name of an event type in the outbox. It is the class name: keep it when you move the class.</summary>
    public static string Name(Type eventType) => eventType.Name;

    /// <summary>The event type of an outbox message, or null for a type that this version of the app does not know.</summary>
    public Type? Find(string name) => _types.GetValueOrDefault(name);

    public IReadOnlyList<Func<IServiceProvider, IDomainEvent, CancellationToken, Task>> HandlersOf(Type eventType) =>
        _handlers.TryGetValue(eventType, out var handlers) ? handlers : [];

    internal bool Add<TEvent, THandler>()
        where TEvent : IDomainEvent
        where THandler : IDomainEventHandler<TEvent>
    {
        if (!_registered.Add((typeof(TEvent), typeof(THandler)))) return false;
        var name = Name(typeof(TEvent));
        if (_types.TryGetValue(name, out var existing) && existing != typeof(TEvent))
            throw new InvalidOperationException($"Two event types have the outbox name '{name}': {existing} and {typeof(TEvent)}.");
        _types[name] = typeof(TEvent);
        if (!_handlers.TryGetValue(typeof(TEvent), out var handlers)) _handlers[typeof(TEvent)] = handlers = [];
        handlers.Add((services, domainEvent, cancellationToken) =>
            services.GetRequiredService<THandler>().Handle((TEvent)domainEvent, cancellationToken));
        return true;
    }

    /// <summary>An event type with no handler yet. Its messages are stored and marked as processed.</summary>
    internal void AddType<TEvent>() where TEvent : IDomainEvent => _types.TryAdd(Name(typeof(TEvent)), typeof(TEvent));
}
