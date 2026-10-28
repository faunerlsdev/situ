using Situ.Domain.Common;

namespace Situ.Domain.Live;

/// <summary>
/// Short-lived slot used only to count viewers. It stores neither the viewer nor an IP address.
/// </summary>
public sealed class LiveViewerSession : AuditableEntity
{
    public static readonly TimeSpan Retention = TimeSpan.FromMinutes(10);

    private LiveViewerSession()
    {
    }

    public Guid LiveStreamId { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    public static LiveViewerSession Open(Guid liveStreamId, DateTimeOffset now, TimeSpan ttl)
    {
        var session = new LiveViewerSession
        {
            LiveStreamId = Guard.NotEmpty(liveStreamId, nameof(liveStreamId)),
            ExpiresAt = now.Add(ttl),
        };
        session.MarkCreated(now);
        return session;
    }
}
