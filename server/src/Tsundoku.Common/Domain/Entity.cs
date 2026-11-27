namespace Tsundoku.Common.Domain;

/// <summary>An object with an identity that stays the same when its values change. Two entities are equal when their ids are equal.</summary>
public abstract class Entity<TId> : IEquatable<Entity<TId>> where TId : struct, IEquatable<TId>
{
    public TId Id { get; protected init; }

    public bool Equals(Entity<TId>? other) => other is not null && other.GetType() == GetType() && other.Id.Equals(Id);

    public override bool Equals(object? obj) => obj is Entity<TId> other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
