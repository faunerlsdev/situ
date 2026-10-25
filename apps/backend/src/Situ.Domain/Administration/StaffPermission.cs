using Situ.Domain.Common;

namespace Situ.Domain.Administration;

/// <summary>Permission granted to a role, expressed as a code such as "categories.manage" or "complaints.resolve".</summary>
public sealed class StaffPermission : AuditableEntity
{
    private StaffPermission()
    {
        Code = null!;
    }

    public Guid RoleId { get; private set; }
    public string Code { get; private set; }

    public static StaffPermission Grant(Guid roleId, string code) => new()
    {
        RoleId = Guard.NotEmpty(roleId, nameof(roleId)),
        Code = Guard.NotEmpty(code, nameof(code), 100).ToLowerInvariant(),
    };
}

public static class StaffPermissionCodes
{
    public const string CategoriesManage = "categories.manage";
    public const string UsersView = "users.view";
    public const string UsersSuspend = "users.suspend";
    public const string ComplaintsResolve = "complaints.resolve";
    public const string LiveCut = "live.cut";
    public const string AnonymityReveal = "anonymity.reveal";
    public const string PlansManage = "plans.manage";
    public const string SettingsManage = "settings.manage";
    public const string StaffManage = "staff.manage";
    public const string AuditView = "audit.view";
    public const string OrganizationsVerify = "organizations.verify";
    public const string TakedownsHandle = "takedowns.handle";
    public const string AvatarsManage = "avatars.manage";
    public const string LegalHoldsManage = "legalholds.manage";
    public const string MetricsView = "metrics.view";
    public const string UsersViewEmail = "users.view_email";
    public const string PlansView = "plans.view";

    public static readonly string[] All =
    [
        CategoriesManage,
        UsersView,
        UsersSuspend,
        ComplaintsResolve,
        LiveCut,
        AnonymityReveal,
        PlansManage,
        PlansView,
        SettingsManage,
        StaffManage,
        AuditView,
        OrganizationsVerify,
        TakedownsHandle,
        AvatarsManage,
        LegalHoldsManage,
        MetricsView,
        UsersViewEmail,
    ];
}
