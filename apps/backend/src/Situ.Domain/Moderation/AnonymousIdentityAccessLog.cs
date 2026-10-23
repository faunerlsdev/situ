using Situ.Domain.Common;

namespace Situ.Domain.Moderation;

/// <summary>
/// Every time staff reveals who is behind an anonymous post it is logged here, with a reason and the legal basis
/// (complaint or court order). Revealing without a record is not possible (RF-ANO-08).
/// </summary>
public sealed class AnonymousIdentityAccessLog : AuditableEntity
{
    private AnonymousIdentityAccessLog()
    {
        Justification = null!;
    }

    public Guid StaffId { get; private set; }
    public ContentTargetType TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public Guid? ComplaintId { get; private set; }
    public string Justification { get; private set; }

    public static AnonymousIdentityAccessLog Record(
        Guid staffId,
        ContentTargetType targetType,
        Guid targetId,
        Guid? complaintId,
        string justification,
        DateTimeOffset now)
    {
        var log = new AnonymousIdentityAccessLog
        {
            StaffId = Guard.NotEmpty(staffId, nameof(staffId)),
            TargetType = targetType,
            TargetId = Guard.NotEmpty(targetId, nameof(targetId)),
            ComplaintId = complaintId,
            Justification = Guard.NotEmpty(justification, nameof(justification), 1000),
        };

        log.MarkCreated(now);
        return log;
    }
}
