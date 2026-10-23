using Situ.Domain.Common;
using Situ.Domain.Guardianship;

namespace Situ.Domain.Live;

public enum LiveStreamStatus
{
    Live,
    Ended,
    Cut,
    Expired,
    Removed,
}

/// <summary>
/// Live video from the mobile app (never from the web). Appears on the map with a red camera icon (RF-LIV-01..09).
/// Minors can only stream to friends and always anonymously (RF-MIN-09).
/// </summary>
public sealed class LiveStream : AggregateRoot
{
    /// <summary>Saved recordings disappear after this period (configurable in system settings).</summary>
    public static readonly TimeSpan DefaultRecordingRetention = TimeSpan.FromDays(60);

    /// <summary>Internal copy kept for moderation/legal purposes after the public copy disappears.</summary>
    public static readonly TimeSpan DefaultInternalCopyRetention = TimeSpan.FromDays(7);

    private LiveStream()
    {
        Title = string.Empty;
    }

    public Guid StreamerId { get; private set; }
    public Guid CategoryId { get; private set; }
    public Guid? CityId { get; private set; }
    public GeoPoint Location { get; private set; }
    public string Title { get; private set; }
    public ContentVisibility Visibility { get; private set; }
    public bool IsAnonymous { get; private set; }
    public bool StreamerIsMinor { get; private set; }

    /// <summary>Anonymous streams blur faces and distort voice (RF-ANO-06).</summary>
    public bool FacesBlurred { get; private set; }

    public bool VoiceModified { get; private set; }
    public LiveStreamStatus Status { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }
    public int PeakViewers { get; private set; }

    public string? IngestKey { get; private set; }
    public string? RecordingKey { get; private set; }
    public DateTimeOffset? RecordingExpiresAt { get; private set; }
    public DateTimeOffset? InternalCopyExpiresAt { get; private set; }
    public Guid? ReportId { get; private set; }

    public bool IsLive => Status == LiveStreamStatus.Live;

    public static LiveStream Start(
        Guid streamerId,
        int streamerAge,
        Guid categoryId,
        Guid? cityId,
        GeoPoint location,
        string title,
        ContentVisibility requestedVisibility,
        bool requestedAnonymous,
        string ingestKey,
        DateTimeOffset now)
    {
        var isMinor = MinorProtectionPolicy.IsMinor(streamerAge);

        // Minors: friends only and always anonymous. Reject instead of silently changing what they asked for.
        Guard.That(
            MinorProtectionPolicy.CanStreamTo(streamerAge, requestedVisibility),
            "live.minor_friends_only",
            "Users under 18 can only stream to their friends.");

        var anonymous = isMinor || requestedAnonymous;

        var stream = new LiveStream
        {
            StreamerId = Guard.NotEmpty(streamerId, nameof(streamerId)),
            CategoryId = Guard.NotEmpty(categoryId, nameof(categoryId)),
            CityId = cityId,
            Location = location,
            Title = Guard.NotEmpty(title, nameof(title), 120),
            Visibility = requestedVisibility,
            IsAnonymous = anonymous,
            StreamerIsMinor = isMinor,
            FacesBlurred = anonymous,
            VoiceModified = anonymous,
            Status = LiveStreamStatus.Live,
            StartedAt = now,
            IngestKey = Guard.NotEmpty(ingestKey, nameof(ingestKey), 200),
        };

        stream.MarkCreated(now);
        stream.Raise(new LiveStreamStartedEvent(stream.Id, categoryId, cityId, location, requestedVisibility, anonymous));
        return stream;
    }

    public void ReportViewerCount(int currentViewers)
    {
        if (currentViewers > PeakViewers)
        {
            PeakViewers = currentViewers;
        }
    }

    public void End(string? recordingKey, DateTimeOffset now, TimeSpan? recordingRetention = null, TimeSpan? internalRetention = null)
    {
        EnsureLive();
        Finish(LiveStreamStatus.Ended, recordingKey, now, recordingRetention, internalRetention);
    }

    /// <summary>Server-side cut by a moderator or by the system (RF-LIV-08).</summary>
    public void Cut(string reason, string? recordingKey, DateTimeOffset now)
    {
        EnsureLive();
        Guard.NotEmpty(reason, nameof(reason), 300);
        Finish(LiveStreamStatus.Cut, recordingKey, now, null, null);
        Raise(new LiveStreamCutEvent(Id, StreamerId, reason));
    }

    /// <summary>Publishes the saved recording as a report. It expires with the recording.</summary>
    public void SaveAsReport(Guid reportId, DateTimeOffset now)
    {
        Guard.That(Status == LiveStreamStatus.Ended, "live.not_ended", "Only ended streams can be saved as a report.");
        Guard.That(RecordingKey is not null, "live.no_recording", "There is no recording to save.");
        Guard.That(ReportId is null, "live.already_saved", "The stream was already saved as a report.");

        ReportId = Guard.NotEmpty(reportId, nameof(reportId));
        MarkUpdated(now);
    }

    /// <summary>The streamer chooses not to keep the recording; it is removed from public view right away.</summary>
    public void Discard(DateTimeOffset now)
    {
        Guard.That(Status is LiveStreamStatus.Ended or LiveStreamStatus.Cut, "live.not_finished", "The stream is still live.");
        RecordingExpiresAt = now;
        ReportId = null;
        MarkUpdated(now);
    }

    public bool RecordingIsAvailable(DateTimeOffset now) =>
        RecordingKey is not null && RecordingExpiresAt is { } exp && now < exp && Status != LiveStreamStatus.Removed;

    public void ExpireRecordingIfDue(DateTimeOffset now)
    {
        if (RecordingExpiresAt is { } exp && now >= exp && Status is LiveStreamStatus.Ended or LiveStreamStatus.Cut)
        {
            Status = LiveStreamStatus.Expired;
            MarkUpdated(now);
        }
    }

    public void Remove(DateTimeOffset now)
    {
        Status = LiveStreamStatus.Removed;
        MarkUpdated(now);
    }

    private void Finish(LiveStreamStatus status, string? recordingKey, DateTimeOffset now, TimeSpan? recordingRetention, TimeSpan? internalRetention)
    {
        Status = status;
        EndedAt = now;
        IngestKey = null;
        RecordingKey = Guard.Optional(recordingKey, nameof(recordingKey), 500);

        if (RecordingKey is not null)
        {
            RecordingExpiresAt = now.Add(recordingRetention ?? DefaultRecordingRetention);
            InternalCopyExpiresAt = RecordingExpiresAt.Value.Add(internalRetention ?? DefaultInternalCopyRetention);
        }

        MarkUpdated(now);
        Raise(new LiveStreamEndedEvent(Id, StreamerId));
    }

    private void EnsureLive() =>
        Guard.That(Status == LiveStreamStatus.Live, "live.not_live", "The stream is not live.");
}

public sealed record LiveStreamStartedEvent(
    Guid LiveStreamId,
    Guid CategoryId,
    Guid? CityId,
    GeoPoint Location,
    ContentVisibility Visibility,
    bool IsAnonymous) : DomainEvent;

public sealed record LiveStreamEndedEvent(Guid LiveStreamId, Guid StreamerId) : DomainEvent;

public sealed record LiveStreamCutEvent(Guid LiveStreamId, Guid StreamerId, string Reason) : DomainEvent;
