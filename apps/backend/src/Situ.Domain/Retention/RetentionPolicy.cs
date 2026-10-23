using Situ.Domain.Common;

namespace Situ.Domain.Retention;

public enum RetentionDataKind
{
    LiveRecording,
    LiveInternalCopy,
    Report,
    ReportMedia,
    Comment,
    LoginAttempt,
    Notification,
    AuditLog,
}

/// <summary>How long each kind of data is kept before the cleanup jobs delete it (RF-RET-01..03).</summary>
public sealed class RetentionPolicy : AuditableEntity
{
    private RetentionPolicy()
    {
    }

    public RetentionDataKind Kind { get; private set; }
    public int RetainDays { get; private set; }
    public bool IsActive { get; private set; }

    public static RetentionPolicy Create(RetentionDataKind kind, int retainDays, DateTimeOffset now)
    {
        var policy = new RetentionPolicy
        {
            Kind = kind,
            RetainDays = Guard.InRange(retainDays, 1, 3650, nameof(retainDays)),
            IsActive = true,
        };

        policy.MarkCreated(now);
        return policy;
    }

    public void ChangeRetention(int retainDays, DateTimeOffset now)
    {
        RetainDays = Guard.InRange(retainDays, 1, 3650, nameof(retainDays));
        MarkUpdated(now);
    }

    public DateTimeOffset ExpiryFor(DateTimeOffset createdAt) => createdAt.AddDays(RetainDays);
}
