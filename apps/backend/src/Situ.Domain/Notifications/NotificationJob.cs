using Situ.Domain.Common;

namespace Situ.Domain.Notifications;

public enum NotificationJobStatus
{
    Pending,
    Processed,
    Failed,
}

/// <summary>Outbox row. The request that created the report only inserts this; a background worker fans out later.</summary>
public sealed class NotificationJob : AuditableEntity
{
    public const string ReportCreated = "report.created";
    public const string CommentAdded = "comment.added";
    public const string LiveStarted = "live.started";
    public const string LiveEnded = "live.ended";
    public const int MaxAttempts = 5;

    private NotificationJob()
    {
        Kind = null!;
    }

    public string Kind { get; private set; }
    public Guid SubjectId { get; private set; }
    public NotificationJobStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public DateTimeOffset AvailableAt { get; private set; }
    public string? LastError { get; private set; }

    public static NotificationJob Enqueue(string kind, Guid subjectId, DateTimeOffset now)
    {
        var job = new NotificationJob
        {
            Kind = Guard.NotEmpty(kind, nameof(kind), 40),
            SubjectId = Guard.NotEmpty(subjectId, nameof(subjectId)),
            Status = NotificationJobStatus.Pending,
            AvailableAt = now,
        };
        job.MarkCreated(now);
        return job;
    }

    public void Complete(DateTimeOffset now)
    {
        Status = NotificationJobStatus.Processed;
        LastError = null;
        MarkUpdated(now);
    }

    public void Retry(DateTimeOffset now, string error)
    {
        Attempts++;
        LastError = Guard.Optional(error, nameof(error), 300);
        if (Attempts >= MaxAttempts)
        {
            Status = NotificationJobStatus.Failed;
        }
        else
        {
            Status = NotificationJobStatus.Pending;
            AvailableAt = now.AddSeconds(30 * Attempts);
        }

        MarkUpdated(now);
    }
}
