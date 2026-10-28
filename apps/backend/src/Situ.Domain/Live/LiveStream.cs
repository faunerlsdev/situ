using Situ.Domain.Common;
using Situ.Domain.Guardianship;

namespace Situ.Domain.Live;

public enum LiveStreamStatus
{
    Waiting,
    Live,
    Ended,
    Cut,
    Expired,
    Removed,
}

/// <summary>
/// Live video from the mobile app (never from the web). Appears on the map with a red camera icon (RF-LIV-01..09).
/// v1 does not let minors stream unless the panel setting live.minors_can_stream is on. When it is, they stay friends-only and anonymous (RF-MIN-09).
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
    public Guid? CategoryId { get; private set; }
    public Guid? CityId { get; private set; }
    public GeoPoint? Location { get; private set; }
    public string? PublicPath { get; private set; }
    public string? PublishKeyHash { get; private set; }
    public DateTimeOffset? PublishKeyExpiresAt { get; private set; }
    public DateTimeOffset? PublishKeyUsedAt { get; private set; }
    public bool HiddenByStaff { get; private set; }
    public string? HiddenReason { get; private set; }
    public string? RecordingPath { get; private set; }
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

    public bool IsOpen => Status is LiveStreamStatus.Waiting or LiveStreamStatus.Live;

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

    /// <summary>
    /// Opens a stream that is not public until MediaMTX reports a publisher.
    /// The plaintext publish key is not stored.
    /// </summary>
    public static LiveStream Schedule(
        Guid streamerId,
        int streamerAge,
        bool minorsCanStream,
        Guid? categoryId,
        Guid? cityId,
        GeoPoint? location,
        string title,
        ContentVisibility visibility,
        string publicPath,
        string publishKeyHash,
        DateTimeOffset publishKeyExpiresAt,
        DateTimeOffset now)
    {
        var isMinor = MinorProtectionPolicy.IsMinor(streamerAge);
        Guard.That(
            !isMinor || minorsCanStream,
            "live.minors_cannot_stream",
            "Minors cannot start a live stream while live.minors_can_stream is off.");
        Guard.That(
            MinorProtectionPolicy.CanStreamTo(streamerAge, visibility),
            "live.minor_friends_only",
            "Users under 18 can only stream to their friends.");

        var stream = new LiveStream
        {
            StreamerId = Guard.NotEmpty(streamerId, nameof(streamerId)),
            CategoryId = categoryId,
            CityId = cityId,
            Location = location,
            Title = Guard.NotEmpty(title, nameof(title), 80),
            Visibility = visibility,
            IsAnonymous = true,
            StreamerIsMinor = isMinor,
            FacesBlurred = true,
            VoiceModified = true,
            Status = LiveStreamStatus.Waiting,
            StartedAt = now,
            PublicPath = Guard.NotEmpty(publicPath, nameof(publicPath), 64),
            PublishKeyHash = Guard.NotEmpty(publishKeyHash, nameof(publishKeyHash), 128),
            PublishKeyExpiresAt = publishKeyExpiresAt,
        };
        stream.MarkCreated(now);
        return stream;
    }

    public bool AcceptPublishKey(string hash, DateTimeOffset now)
    {
        if (PublishRefusal(hash, now) is not null)
        {
            return false;
        }

        PublishKeyUsedAt = now;
        MarkUpdated(now);
        return true;
    }

    /// <summary>
    /// Why a publish key cannot be accepted. Null means the hash matches and the key can be consumed.
    /// The password itself is never part of the code.
    /// </summary>
    public string? PublishRefusal(string? password, string hash, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return "live.missing_key";
        }

        return PublishRefusal(hash, now);
    }

    private string? PublishRefusal(string hash, DateTimeOffset now)
    {
        if (HiddenByStaff)
        {
            return "live.invalid_state";
        }

        if (Status == LiveStreamStatus.Expired)
        {
            return "live.limit";
        }

        if (PublishKeyUsedAt is not null)
        {
            return "live.already_used";
        }

        if (Status != LiveStreamStatus.Waiting)
        {
            return "live.invalid_state";
        }

        if (PublishKeyExpiresAt is not { } expires || now > expires)
        {
            return "live.expired";
        }

        if (!FixedEquals(PublishKeyHash, hash))
        {
            return "live.invalid_key";
        }

        return null;
    }

    public bool MarkLive(DateTimeOffset now)
    {
        if (Status != LiveStreamStatus.Waiting || HiddenByStaff || PublishKeyUsedAt is null)
        {
            return false;
        }

        Status = LiveStreamStatus.Live;
        StartedAt = now;
        MarkUpdated(now);
        Raise(new LiveStreamPublishedEvent(Id, PublicPath ?? string.Empty, Visibility, Location));
        return true;
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
        EnsureOpen();
        Finish(LiveStreamStatus.Ended, recordingKey, now, recordingRetention, internalRetention);
    }

    public void Hide(string reason, DateTimeOffset now)
    {
        HiddenByStaff = true;
        HiddenReason = Guard.NotEmpty(reason, nameof(reason), 300);
        if (IsOpen)
        {
            Cut(reason, null, now);
            return;
        }

        MarkUpdated(now);
    }

    /// <summary>Server-side cut by a moderator or by the system (RF-LIV-08).</summary>
    public void Cut(string reason, string? recordingKey, DateTimeOffset now)
    {
        EnsureOpen();
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
        var wasVisible = Status == LiveStreamStatus.Live;
        Status = status;
        EndedAt = now;
        IngestKey = null;
        PublishKeyHash = null;
        RecordingKey = Guard.Optional(recordingKey, nameof(recordingKey), 500);
        RecordingPath = RecordingKey;

        if (RecordingKey is not null)
        {
            RecordingExpiresAt = now.Add(recordingRetention ?? DefaultRecordingRetention);
            InternalCopyExpiresAt = RecordingExpiresAt.Value.Add(internalRetention ?? DefaultInternalCopyRetention);
        }

        MarkUpdated(now);
        Raise(new LiveStreamEndedEvent(Id, StreamerId, wasVisible));
    }

    private void EnsureOpen() =>
        Guard.That(IsOpen, "live.not_live", "The stream is not live.");

    private static bool FixedEquals(string? left, string? right)
    {
        if (left is null || right is null || left.Length != right.Length)
        {
            return false;
        }

        var difference = 0;
        for (var index = 0; index < left.Length; index++)
        {
            difference |= left[index] ^ right[index];
        }

        return difference == 0;
    }
}

public sealed record LiveStreamStartedEvent(
    Guid LiveStreamId,
    Guid CategoryId,
    Guid? CityId,
    GeoPoint Location,
    ContentVisibility Visibility,
    bool IsAnonymous) : DomainEvent;

public sealed record LiveStreamEndedEvent(Guid LiveStreamId, Guid StreamerId, bool WasVisible) : DomainEvent;

public sealed record LiveStreamPublishedEvent(
    Guid LiveStreamId,
    string PublicPath,
    ContentVisibility Visibility,
    GeoPoint? Location) : DomainEvent;

public sealed record LiveStreamCutEvent(Guid LiveStreamId, Guid StreamerId, string Reason) : DomainEvent;
