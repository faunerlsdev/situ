using Situ.Domain.Common;

namespace Situ.Domain.Administration;

/// <summary>Append-only trail of sensitive actions (admin changes, logins to the panel, data access).</summary>
public sealed class AuditLog : Entity
{
    private AuditLog()
    {
        Action = null!;
        EntityType = null!;
    }

    public DateTimeOffset OccurredAt { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string Action { get; private set; }
    public string EntityType { get; private set; }
    public string? EntityId { get; private set; }
    public string? Changes { get; private set; }
    public string? IpAddress { get; private set; }

    public static AuditLog Record(
        Guid? actorUserId,
        string action,
        string entityType,
        string? entityId,
        string? changes,
        string? ipAddress,
        DateTimeOffset now) => new()
    {
        OccurredAt = now,
        ActorUserId = actorUserId,
        Action = Guard.NotEmpty(action, nameof(action), 100),
        EntityType = Guard.NotEmpty(entityType, nameof(entityType), 100),
        EntityId = Guard.Optional(entityId, nameof(entityId), 100),
        Changes = changes,
        IpAddress = Guard.Optional(ipAddress, nameof(ipAddress), 64),
    };
}
