using Situ.Domain.Common;

namespace Situ.Domain.Moderation;

public enum ComplaintReason
{
    FalseInformation,
    Harassment,
    Violence,
    SexualContent,
    MinorSafety,
    PrivacyViolation,
    Spam,
    Other,
}

public enum ComplaintStatus
{
    Open,
    InReview,
    Upheld,
    Dismissed,
}

/// <summary>User complaint ("denuncia") about content or a user (RF-MOD-01). Complaints about minors have top priority.</summary>
public sealed class Complaint : AggregateRoot
{
    private Complaint()
    {
    }

    public Guid ReporterId { get; private set; }
    public ContentTargetType TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public ComplaintReason Reason { get; private set; }
    public string? Details { get; private set; }
    public ComplaintStatus Status { get; private set; }
    public bool IsPriority { get; private set; }
    public Guid? ResolvedByStaffId { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public string? Resolution { get; private set; }

    public static Complaint File(
        Guid reporterId,
        ContentTargetType targetType,
        Guid targetId,
        ComplaintReason reason,
        string? details,
        DateTimeOffset now)
    {
        var complaint = new Complaint
        {
            ReporterId = Guard.NotEmpty(reporterId, nameof(reporterId)),
            TargetType = targetType,
            TargetId = Guard.NotEmpty(targetId, nameof(targetId)),
            Reason = reason,
            Details = Guard.Optional(details, nameof(details), 1000),
            Status = ComplaintStatus.Open,
            IsPriority = reason is ComplaintReason.MinorSafety or ComplaintReason.SexualContent or ComplaintReason.Violence,
        };

        complaint.MarkCreated(now);
        complaint.Raise(new ComplaintFiledEvent(complaint.Id, targetType, complaint.TargetId, reason, complaint.IsPriority));
        return complaint;
    }

    public void StartReview(Guid staffId, DateTimeOffset now)
    {
        Guard.That(Status == ComplaintStatus.Open, "complaint.not_open", "The complaint is not open.");
        Status = ComplaintStatus.InReview;
        ResolvedByStaffId = Guard.NotEmpty(staffId, nameof(staffId));
        MarkUpdated(now);
    }

    public void Uphold(Guid staffId, string resolution, DateTimeOffset now) => Resolve(ComplaintStatus.Upheld, staffId, resolution, now);

    public void Dismiss(Guid staffId, string resolution, DateTimeOffset now) => Resolve(ComplaintStatus.Dismissed, staffId, resolution, now);

    private void Resolve(ComplaintStatus status, Guid staffId, string resolution, DateTimeOffset now)
    {
        Guard.That(Status is ComplaintStatus.Open or ComplaintStatus.InReview, "complaint.closed", "The complaint is already resolved.");
        Status = status;
        ResolvedByStaffId = Guard.NotEmpty(staffId, nameof(staffId));
        Resolution = Guard.NotEmpty(resolution, nameof(resolution), 1000);
        ResolvedAt = now;
        MarkUpdated(now);
        Raise(new ComplaintResolvedEvent(Id, ReporterId, status));
    }
}

/// <summary>
/// The reporter should get a generic notice. The log must not include who was reported.
/// </summary>
public sealed record ComplaintResolvedEvent(Guid ComplaintId, Guid ReporterId, ComplaintStatus Status) : DomainEvent;

public sealed record ComplaintFiledEvent(Guid ComplaintId, ContentTargetType TargetType, Guid TargetId, ComplaintReason Reason, bool IsPriority) : DomainEvent;
