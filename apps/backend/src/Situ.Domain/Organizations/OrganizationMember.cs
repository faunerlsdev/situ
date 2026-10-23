using Situ.Domain.Common;

namespace Situ.Domain.Organizations;

public enum OrganizationRole
{
    Owner,
    Admin,
    Editor,
}

public sealed class OrganizationMember : AuditableEntity
{
    private OrganizationMember()
    {
    }

    public Guid OrganizationId { get; private set; }
    public Guid UserId { get; private set; }
    public OrganizationRole Role { get; private set; }

    public static OrganizationMember Add(Guid organizationId, Guid userId, OrganizationRole role, DateTimeOffset now)
    {
        var member = new OrganizationMember
        {
            OrganizationId = Guard.NotEmpty(organizationId, nameof(organizationId)),
            UserId = Guard.NotEmpty(userId, nameof(userId)),
            Role = role,
        };

        member.MarkCreated(now);
        return member;
    }

    public void ChangeRole(OrganizationRole role, DateTimeOffset now)
    {
        Role = role;
        MarkUpdated(now);
    }
}
