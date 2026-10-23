using Situ.Domain.Common;

namespace Situ.Domain.Retention;

/// <summary>Freezes deletion of something because of a legal or judicial requirement.</summary>
public sealed class LegalHold : AggregateRoot
{
    private LegalHold()
    {
        Reason = null!;
    }

    public ContentTargetType TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public string Reason { get; private set; }
    public Guid PlacedByStaffId { get; private set; }
    public DateTimeOffset? ReleasedAt { get; private set; }

    public bool IsActive => ReleasedAt is null;

    public static LegalHold Place(ContentTargetType targetType, Guid targetId, string reason, Guid staffId, DateTimeOffset now)
    {
        var hold = new LegalHold
        {
            TargetType = targetType,
            TargetId = Guard.NotEmpty(targetId, nameof(targetId)),
            Reason = Guard.NotEmpty(reason, nameof(reason), 500),
            PlacedByStaffId = Guard.NotEmpty(staffId, nameof(staffId)),
        };

        hold.MarkCreated(now);
        return hold;
    }

    public void Release(DateTimeOffset now)
    {
        if (ReleasedAt is null)
        {
            ReleasedAt = now;
            MarkUpdated(now);
        }
    }
}
