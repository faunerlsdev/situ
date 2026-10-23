using Situ.Domain.Common;

namespace Situ.Domain.Moderation;

public enum TakedownStatus
{
    Received,
    UnderReview,
    Accepted,
    Rejected,
}

/// <summary>
/// Request to remove content or data coming from outside (people who appear in content, habeas data claims, authorities).
/// Habeas data (Ley 1581 de 2012) response deadlines are tracked through <see cref="DueAt"/>.
/// </summary>
public sealed class TakedownRequest : AggregateRoot
{
    /// <summary>Ley 1581 art. 15: claims must be answered within 15 business days; we use calendar days as a safe margin.</summary>
    public static readonly TimeSpan DefaultResponseWindow = TimeSpan.FromDays(15);

    private TakedownRequest()
    {
        RequesterName = null!;
        RequesterEmail = null!;
        Description = null!;
    }

    public string RequesterName { get; private set; }
    public string RequesterEmail { get; private set; }
    public ContentTargetType TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public string Description { get; private set; }
    public TakedownStatus Status { get; private set; }
    public DateTimeOffset DueAt { get; private set; }
    public Guid? HandledByStaffId { get; private set; }
    public string? Resolution { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }

    public static TakedownRequest Receive(
        string requesterName,
        string requesterEmail,
        ContentTargetType targetType,
        Guid targetId,
        string description,
        DateTimeOffset now)
    {
        var request = new TakedownRequest
        {
            RequesterName = Guard.NotEmpty(requesterName, nameof(requesterName), 120),
            RequesterEmail = Email.Create(requesterEmail).Value,
            TargetType = targetType,
            TargetId = Guard.NotEmpty(targetId, nameof(targetId)),
            Description = Guard.NotEmpty(description, nameof(description), 2000),
            Status = TakedownStatus.Received,
            DueAt = now.Add(DefaultResponseWindow),
        };

        request.MarkCreated(now);
        return request;
    }

    public bool IsOverdue(DateTimeOffset now) =>
        Status is TakedownStatus.Received or TakedownStatus.UnderReview && now > DueAt;

    public void StartReview(Guid staffId, DateTimeOffset now)
    {
        Guard.That(Status == TakedownStatus.Received, "takedown.not_received", "The request is already being handled.");
        Status = TakedownStatus.UnderReview;
        HandledByStaffId = Guard.NotEmpty(staffId, nameof(staffId));
        MarkUpdated(now);
    }

    public void Accept(string resolution, DateTimeOffset now) => Close(TakedownStatus.Accepted, resolution, now);

    public void Reject(string resolution, DateTimeOffset now) => Close(TakedownStatus.Rejected, resolution, now);

    private void Close(TakedownStatus status, string resolution, DateTimeOffset now)
    {
        Guard.That(Status == TakedownStatus.UnderReview, "takedown.not_in_review", "The request must be under review.");
        Status = status;
        Resolution = Guard.NotEmpty(resolution, nameof(resolution), 2000);
        ResolvedAt = now;
        MarkUpdated(now);
    }
}
