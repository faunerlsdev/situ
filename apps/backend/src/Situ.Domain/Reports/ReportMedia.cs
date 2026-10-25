using Situ.Domain.Common;

namespace Situ.Domain.Reports;

/// <summary>Photo or video attached to a report. Files live in object storage; only the key is stored.</summary>
public sealed class ReportMedia : AuditableEntity
{
    private ReportMedia()
    {
        StorageKey = null!;
    }

    public Guid? ReportId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public MediaType Type { get; private set; }
    public string StorageKey { get; private set; }
    public string? ThumbnailKey { get; private set; }
    public long SizeBytes { get; private set; }
    public int? DurationSeconds { get; private set; }
    public int SortOrder { get; private set; }
    public MediaProcessingStatus ProcessingStatus { get; private set; }

    /// <summary>Faces blurred automatically for anonymous or minor authors.</summary>
    public bool FacesBlurred { get; private set; }

    public static ReportMedia Create(
        Guid reportId,
        Guid ownerUserId,
        MediaType type,
        string storageKey,
        long sizeBytes,
        int? durationSeconds,
        int sortOrder,
        bool facesBlurred,
        DateTimeOffset now)
    {
        var media = Stage(ownerUserId, type, storageKey, sizeBytes, durationSeconds, facesBlurred, now);
        media.Attach(reportId, sortOrder);
        return media;
    }

    /// <summary>Upload that is not part of a report yet. A job deletes it if it stays unattached.</summary>
    public static ReportMedia Stage(
        Guid ownerUserId,
        MediaType type,
        string storageKey,
        long sizeBytes,
        int? durationSeconds,
        bool facesBlurred,
        DateTimeOffset now)
    {
        Guard.That(sizeBytes > 0, "report_media.empty", "The file is empty.");

        var media = new ReportMedia
        {
            OwnerUserId = Guard.NotEmpty(ownerUserId, nameof(ownerUserId)),
            Type = type,
            StorageKey = Guard.NotEmpty(storageKey, nameof(storageKey), 500),
            SizeBytes = sizeBytes,
            DurationSeconds = type == MediaType.Video ? durationSeconds : null,
            SortOrder = 0,
            FacesBlurred = facesBlurred,
            ProcessingStatus = MediaProcessingStatus.Pending,
        };

        media.MarkCreated(now);
        return media;
    }

    public void Attach(Guid reportId, int sortOrder)
    {
        ReportId = Guard.NotEmpty(reportId, nameof(reportId));
        SortOrder = sortOrder;
    }

    public void MarkReady(string? thumbnailKey)
    {
        ThumbnailKey = Guard.Optional(thumbnailKey, nameof(thumbnailKey), 500);
        ProcessingStatus = MediaProcessingStatus.Ready;
    }

    public void MarkFailed() => ProcessingStatus = MediaProcessingStatus.Failed;
}
