using Microsoft.Extensions.DependencyInjection.Extensions;
using Tsundoku.Common.Domain;

namespace Tsundoku.Common.Events;

public static class DomainEventRegistration
{
    /// <summary>Adds the registry and the dispatcher. A second call changes nothing.</summary>
    public static IServiceCollection AddDomainEvents(this IServiceCollection services)
    {
        if (Registry(services) is not null) return services;
        services.AddSingleton(new DomainEventRegistry());
        services.AddSingleton<DomainEventDispatcher>();
        return services;
    }

    /// <summary>Runs <typeparamref name="THandler"/> for each committed <typeparamref name="TEvent"/>.</summary>
    public static IServiceCollection AddDomainEventHandler<TEvent, THandler>(this IServiceCollection services)
        where TEvent : IDomainEvent
        where THandler : class, IDomainEventHandler<TEvent>
    {
        if (Require(services).Add<TEvent, THandler>()) services.TryAddScoped<THandler>();
        return services;
    }

    /// <summary>Makes an event type known to the outbox before it has a handler.</summary>
    public static IServiceCollection AddDomainEvent<TEvent>(this IServiceCollection services) where TEvent : IDomainEvent
    {
        Require(services).AddType<TEvent>();
        return services;
    }

    private static DomainEventRegistry Require(IServiceCollection services) =>
        Registry(services) ?? throw new InvalidOperationException("Call AddDomainEvents() before adding events.");

    private static DomainEventRegistry? Registry(IServiceCollection services) =>
        services.SingleOrDefault(d => d.ServiceType == typeof(DomainEventRegistry))?.ImplementationInstance as DomainEventRegistry;
}
