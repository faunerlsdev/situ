using Situ.Domain.Common;

namespace Situ.Domain.Administration;

/// <summary>Role for the admin panel (Super admin, Moderator, Support, Finance...). Permissions are data, not code.</summary>
public sealed class StaffRole : AuditableEntity
{
    private StaffRole()
    {
        Name = null!;
        Description = null!;
    }

    public string Name { get; private set; }
    public string Description { get; private set; }
    public bool IsSystem { get; private set; }

    public static StaffRole Create(string name, string description, bool isSystem, DateTimeOffset now)
    {
        var role = new StaffRole
        {
            Name = Guard.NotEmpty(name, nameof(name), 60),
            Description = Guard.NotEmpty(description, nameof(description), 200),
            IsSystem = isSystem,
        };

        role.MarkCreated(now);
        return role;
    }

    public void Update(string name, string description, DateTimeOffset now)
    {
        Guard.That(!IsSystem, "staff_role.system", "System roles cannot be edited.");
        Name = Guard.NotEmpty(name, nameof(name), 60);
        Description = Guard.NotEmpty(description, nameof(description), 200);
        MarkUpdated(now);
    }
}
