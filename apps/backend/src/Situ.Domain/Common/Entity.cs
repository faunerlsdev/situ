namespace Situ.Domain.Common;

/// <summary>Base class for every domain entity. Identity is a time-ordered GUID (UUIDv7).</summary>
public abstract class Entity : IEquatable<Entity>
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7();

    public bool Equals(Entity? other) =>
        other is not null && GetType() == other.GetType() && Id == other.Id;

    public override bool Equals(object? obj) => obj is Entity other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}

/// <summary>Entity whose creation/update timestamps are maintained automatically on save.</summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; }
    DateTimeOffset? UpdatedAt { get; }
    void MarkCreated(DateTimeOffset now);
    void MarkUpdated(DateTimeOffset now);
}

public abstract class AuditableEntity : Entity, IAuditable
{
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public void MarkCreated(DateTimeOffset now)
    {
        if (CreatedAt == default)
        {
            CreatedAt = now;
        }
    }

    public void MarkUpdated(DateTimeOffset now) => UpdatedAt = now;
}

/// <summary>Consistency boundary. Raises domain events that Infrastructure dispatches after saving.</summary>
public abstract class AggregateRoot : AuditableEntity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
