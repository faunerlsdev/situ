using Situ.Domain.Common;

namespace Situ.Domain.Retention;

public enum DataExportStatus
{
    Requested,
    Processing,
    Ready,
    Failed,
    Expired,
}

/// <summary>Right of access / portability: the user downloads a copy of their data (habeas data).</summary>
public sealed class DataExportRequest : AggregateRoot
{
    public static readonly TimeSpan DownloadWindow = TimeSpan.FromDays(7);

    private DataExportRequest()
    {
    }

    public Guid UserId { get; private set; }
    public DataExportStatus Status { get; private set; }
    public string? FileKey { get; private set; }
    public DateTimeOffset? ReadyAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }

    public static DataExportRequest Create(Guid userId, DateTimeOffset now)
    {
        var request = new DataExportRequest
        {
            UserId = Guard.NotEmpty(userId, nameof(userId)),
            Status = DataExportStatus.Requested,
        };

        request.MarkCreated(now);
        return request;
    }

    public void StartProcessing(DateTimeOffset now)
    {
        Guard.That(Status == DataExportStatus.Requested, "data_export.not_requested", "The export is already being processed.");
        Status = DataExportStatus.Processing;
        MarkUpdated(now);
    }

    public void Complete(string fileKey, DateTimeOffset now)
    {
        Guard.That(Status == DataExportStatus.Processing, "data_export.not_processing", "The export is not being processed.");
        FileKey = Guard.NotEmpty(fileKey, nameof(fileKey), 500);
        Status = DataExportStatus.Ready;
        ReadyAt = now;
        ExpiresAt = now.Add(DownloadWindow);
        MarkUpdated(now);
    }

    public void Fail(DateTimeOffset now)
    {
        Status = DataExportStatus.Failed;
        MarkUpdated(now);
    }

    public void ExpireIfDue(DateTimeOffset now)
    {
        if (Status == DataExportStatus.Ready && ExpiresAt is { } exp && now >= exp)
        {
            Status = DataExportStatus.Expired;
            FileKey = null;
            MarkUpdated(now);
        }
    }
}
