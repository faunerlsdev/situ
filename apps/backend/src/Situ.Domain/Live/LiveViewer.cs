using Situ.Domain.Common;

namespace Situ.Domain.Live;

/// <summary>A user watching (or who watched) a live stream. Used for viewer counts and moderation.</summary>
public sealed class LiveViewer : AuditableEntity
{
    private LiveViewer()
    {
    }

    public Guid LiveStreamId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }
    public DateTimeOffset? LeftAt { get; private set; }

    public bool IsWatching => LeftAt is null;

    public static LiveViewer Join(Guid liveStreamId, Guid userId, DateTimeOffset now)
    {
        var viewer = new LiveViewer
        {
            LiveStreamId = Guard.NotEmpty(liveStreamId, nameof(liveStreamId)),
            UserId = Guard.NotEmpty(userId, nameof(userId)),
            JoinedAt = now,
        };

        viewer.MarkCreated(now);
        return viewer;
    }

    public void Leave(DateTimeOffset now)
    {
        if (LeftAt is null)
        {
            LeftAt = now;
            MarkUpdated(now);
        }
    }
}
