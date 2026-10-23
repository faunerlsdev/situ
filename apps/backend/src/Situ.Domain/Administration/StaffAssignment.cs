using Situ.Domain.Common;

namespace Situ.Domain.Administration;

/// <summary>A user with a role in the admin panel. Staff are regular users with an extra assignment.</summary>
public sealed class StaffAssignment : AuditableEntity
{
    private StaffAssignment()
    {
    }

    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public bool IsActive { get; private set; }
    public Guid? GrantedByUserId { get; private set; }

    public static StaffAssignment Assign(Guid userId, Guid roleId, Guid? grantedByUserId, DateTimeOffset now)
    {
        var assignment = new StaffAssignment
        {
            UserId = Guard.NotEmpty(userId, nameof(userId)),
            RoleId = Guard.NotEmpty(roleId, nameof(roleId)),
            GrantedByUserId = grantedByUserId,
            IsActive = true,
        };

        assignment.MarkCreated(now);
        return assignment;
    }

    public void Revoke(DateTimeOffset now)
    {
        IsActive = false;
        MarkUpdated(now);
    }

    public void Reactivate(DateTimeOffset now)
    {
        IsActive = true;
        MarkUpdated(now);
    }
}
