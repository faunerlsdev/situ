using Situ.Domain.Common;

namespace Situ.Domain.Moderation;

public enum ModerationActionType
{
    HideContent,
    RemoveContent,
    RestoreContent,
    CutLiveStream,
    WarnUser,
    SuspendUser,
    BanUser,
    ReinstateUser,
    ForceLogout,
}

/// <summary>Immutable record of what staff did and why. Part of the audit trail.</summary>
public sealed class ModerationAction : AuditableEntity
{
    private ModerationAction()
    {
        Reason = null!;
    }

    public Guid StaffId { get; private set; }
    public ModerationActionType Action { get; private set; }
    public ContentTargetType TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public Guid? ComplaintId { get; private set; }
    public string Reason { get; private set; }

    public static ModerationAction Record(
        Guid staffId,
        ModerationActionType action,
        ContentTargetType targetType,
        Guid targetId,
        Guid? complaintId,
        string reason,
        DateTimeOffset now)
    {
        var record = new ModerationAction
        {
            StaffId = Guard.NotEmpty(staffId, nameof(staffId)),
            Action = action,
            TargetType = targetType,
            TargetId = Guard.NotEmpty(targetId, nameof(targetId)),
            ComplaintId = complaintId,
            Reason = Guard.NotEmpty(reason, nameof(reason), 1000),
        };

        record.MarkCreated(now);
        return record;
    }
}
