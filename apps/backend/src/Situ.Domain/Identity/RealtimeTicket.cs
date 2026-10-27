using Situ.Domain.Common;

namespace Situ.Domain.Identity;

/// <summary>
/// One-time credential for the map hub. The browser never receives the access token.
/// The plain ticket lasts 30 seconds, is bound to one user and one web device, and dies on use.
/// </summary>
public sealed class RealtimeTicket : AuditableEntity
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

    private RealtimeTicket()
    {
        TokenHash = null!;
    }

    public Guid UserId { get; private set; }
    public Guid DeviceId { get; private set; }
    public string TokenHash { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }

    public static RealtimeTicket Issue(Guid userId, Guid deviceId, string tokenHash, DateTimeOffset now)
    {
        var ticket = new RealtimeTicket
        {
            UserId = Guard.NotEmpty(userId, nameof(userId)),
            DeviceId = Guard.NotEmpty(deviceId, nameof(deviceId)),
            TokenHash = Guard.NotEmpty(tokenHash, nameof(tokenHash), 128),
            ExpiresAt = now.Add(Lifetime),
        };
        ticket.MarkCreated(now);
        return ticket;
    }

    public RealtimeTicketStatus Status(DateTimeOffset now, Guid? expectedUserId)
    {
        if (expectedUserId is { } user && user != UserId)
        {
            return RealtimeTicketStatus.OtherUser;
        }

        if (ConsumedAt is not null)
        {
            return RealtimeTicketStatus.Used;
        }

        if (ExpiresAt <= now)
        {
            return RealtimeTicketStatus.Expired;
        }

        return RealtimeTicketStatus.Valid;
    }

    public void Consume(DateTimeOffset now)
    {
        Guard.That(Status(now, null) == RealtimeTicketStatus.Valid, "realtime_ticket.invalid", "The ticket cannot be used.");
        ConsumedAt = now;
    }
}

public enum RealtimeTicketStatus
{
    Valid,
    Used,
    Expired,
    OtherUser,
}
