using System.Text.Json;
using Tsundoku.Common.Events;
using Tsundoku.Common.Domain;

namespace Tsundoku.Common.Persistence.Outbox;

/// <summary>Domain events to JSON and back. The type name comes from the registry, so it does not depend on the namespace.</summary>
internal sealed class OutboxSerializer(DomainEventRegistry registry)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static (string Type, string Payload) Serialize(IDomainEvent domainEvent) =>
        (DomainEventRegistry.Name(domainEvent.GetType()), JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), Json));

    /// <returns>The event, or null when this version of the app does not know the type.</returns>
    public IDomainEvent? Deserialize(string type, string payload) =>
        registry.Find(type) is { } eventType ? (IDomainEvent?)JsonSerializer.Deserialize(payload, eventType, Json) : null;
}
