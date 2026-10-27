using Situ.Api.Auth;
using Situ.Api.Http;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Administration.Auth;
using Situ.Application.Features.Administration.Audit;
using Situ.Application.Features.Administration.Catalog;
using Situ.Application.Features.Administration.Complaints;
using Situ.Application.Features.Administration.GetAdminSession;
using Situ.Application.Features.Administration.Retention;
using Situ.Application.Features.Administration.Settings;
using Situ.Application.Features.Administration.Staff;
using Situ.Application.Features.Administration.Takedowns;
using Situ.Application.Features.Administration.Users;
using Situ.Application.Features.Reports.ManageReportCategories;

namespace Situ.Api.Endpoints.Administration;

public sealed class AdminEndpoints : IEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/admin/me", Me)
            .WithName("GetAdminMe")
            .WithSummary("Rol y permisos efectivos del personal, leídos de la base.")
            .Produces<GetAdminMeResponse>()
            .WithTags("Admin")
            .RequireAuthorization(AuthorizationPolicies.Staff)
            .RequireRateLimiting("admin");

        var admin = group.MapGroup("/admin")
            .RequireAuthorization(AuthorizationPolicies.StaffMfa)
            .RequireRateLimiting("admin")
            .WithTags("Admin");

        admin.MapPost("/2fa/reset", ResetTwoFactor).WithName("ResetStaffTwoFactor").WithSummary("Un superadministrador restablece el segundo factor de otra cuenta.");
        admin.MapGet("/dashboard", Dashboard).WithName("GetAdminDashboard").Produces<GetAdminDashboardResponse>().WithSummary("Agregados del panel. Sin datos personales.");
        admin.MapGet("/complaints/{id:guid}", Complaint).WithName("GetAdminComplaint").WithSummary("Contexto para resolver una denuncia. El autor anónimo no se revela.");
        admin.MapGet("/users", Users).WithName("SearchAdminUsers").RequireRateLimiting("admin-user-lookup").WithSummary("Busca una cuenta por usuario o por correo exacto.");
        admin.MapGet("/users/{id:guid}", User).WithName("GetAdminUser").WithSummary("Ficha de una cuenta. El correo va enmascarado salvo users.view_email.");
        admin.MapPost("/users/{id:guid}/warn", (Guid id, ReasonBody body, HttpContext http, ICurrentUser current, IHandler<SanctionUserRequest, Result> handler, CancellationToken cancellationToken) =>
            Sanction(id, "warn", body, http, current, handler, cancellationToken)).WithName("WarnAdminUser");
        admin.MapPost("/users/{id:guid}/suspend", (Guid id, SuspendBody body, HttpContext http, ICurrentUser current, IHandler<SanctionUserRequest, Result> handler, CancellationToken cancellationToken) =>
            Sanction(id, "suspend", new ReasonBody(body.Reason, body.Until), http, current, handler, cancellationToken)).WithName("SuspendAdminUser");
        admin.MapPost("/users/{id:guid}/ban", (Guid id, ReasonBody body, HttpContext http, ICurrentUser current, IHandler<SanctionUserRequest, Result> handler, CancellationToken cancellationToken) =>
            Sanction(id, "ban", body, http, current, handler, cancellationToken)).WithName("BanAdminUser");
        admin.MapPost("/users/{id:guid}/reinstate", (Guid id, ReasonBody body, HttpContext http, ICurrentUser current, IHandler<SanctionUserRequest, Result> handler, CancellationToken cancellationToken) =>
            Sanction(id, "reinstate", body, http, current, handler, cancellationToken)).WithName("ReinstateAdminUser");
        admin.MapPost("/users/{id:guid}/force-logout", (Guid id, ReasonBody body, HttpContext http, ICurrentUser current, IHandler<SanctionUserRequest, Result> handler, CancellationToken cancellationToken) =>
            Sanction(id, "force-logout", body, http, current, handler, cancellationToken)).WithName("ForceLogoutAdminUser");

        admin.MapGet("/categories", Categories).WithName("ListAdminCategories").WithSummary("Catálogo completo, con vigencia, orden y reportes activos.").Produces<ListAdminCategoriesResponse>();
        admin.MapGet("/categories/{id:guid}", Category).WithName("GetAdminCategory").WithSummary("Una categoría con su vigencia, orden y reportes activos.").Produces<AdminCategoryView>();
        admin.MapPost("/categories", CreateCategory).WithName("AdminCreateCategory");
        admin.MapPut("/categories/{id:guid}", UpdateCategory).WithName("AdminUpdateCategory");
        admin.MapPost("/categories/{id:guid}/active", SetActive).WithName("AdminSetCategoryActive");
        admin.MapPut("/categories/order", Reorder).WithName("AdminReorderCategories");
        admin.MapPut("/categories/{id:guid}/overrides", Override).WithName("AdminUpsertCategoryOverride");
        admin.MapPut("/categories/{id:guid}/translations", Translation).WithName("AdminUpsertCategoryTranslation");
        admin.MapPost("/categories/{id:guid}/icons", Icons).WithName("UploadCategoryIcons").DisableAntiforgery();

        admin.MapGet("/avatars", Avatars).WithName("ListAdminAvatars");
        admin.MapPost("/avatars", SaveAvatar).WithName("CreateAdminAvatar").DisableAntiforgery();
        admin.MapPut("/avatars/{id:guid}", SaveAvatar).WithName("UpdateAdminAvatar").DisableAntiforgery();
        admin.MapPost("/avatars/{id:guid}/active", AvatarActive).WithName("SetAdminAvatarActive");

        admin.MapGet("/plans", Plans).WithName("ListAdminPlans").WithSummary("Planes en solo lectura.");
        admin.MapGet("/settings", Settings).WithName("ListAdminSettings");
        admin.MapPut("/settings/{key}", UpdateSetting).WithName("UpdateAdminSetting");
        admin.MapPut("/countries/{id:guid}/minimum-age", MinimumAge).WithName("ChangeCountryMinimumAge");

        admin.MapGet("/legal-holds", Holds).WithName("ListLegalHolds");
        admin.MapPost("/legal-holds", PlaceHold).WithName("PlaceLegalHold");
        admin.MapPost("/legal-holds/{id:guid}/release", ReleaseHold).WithName("ReleaseLegalHold");
        admin.MapGet("/retention-policies", Retention).WithName("ListRetentionPolicies");
        admin.MapPut("/retention-policies/{kind}", UpdateRetention).WithName("UpdateRetentionPolicy");

        admin.MapGet("/takedowns", Takedowns).WithName("ListAdminTakedowns");
        admin.MapPost("/takedowns/{id:guid}/start-review", (Guid id, HttpContext http, ICurrentUser current, IHandler<ReviewTakedownRequest, Result> handler, CancellationToken cancellationToken) =>
            Review(id, "start-review", null, http, current, handler, cancellationToken)).WithName("StartTakedownReview");
        admin.MapPost("/takedowns/{id:guid}/accept", (Guid id, ResolutionBody body, HttpContext http, ICurrentUser current, IHandler<ReviewTakedownRequest, Result> handler, CancellationToken cancellationToken) =>
            Review(id, "accept", body.Resolution, http, current, handler, cancellationToken)).WithName("AcceptTakedown");
        admin.MapPost("/takedowns/{id:guid}/reject", (Guid id, ResolutionBody body, HttpContext http, ICurrentUser current, IHandler<ReviewTakedownRequest, Result> handler, CancellationToken cancellationToken) =>
            Review(id, "reject", body.Resolution, http, current, handler, cancellationToken)).WithName("RejectTakedown");

        admin.MapGet("/audit-logs", Audit).WithName("ListAuditLogs");
        admin.MapGet("/anonymous-identity-access-logs", AnonymousLogs).WithName("ListAnonymousIdentityAccessLogs");
        admin.MapGet("/staff", Staff).WithName("ListAdminStaff");
        admin.MapPost("/staff", Assign).WithName("AssignStaffRole");
        admin.MapDelete("/staff/{id:guid}", Revoke).WithName("RevokeStaffRole");
    }

    private static async Task<IResult> Me(ICurrentUser current, IHandler<GetAdminMeRequest, Result<GetAdminMeResponse>> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new GetAdminMeRequest(current.UserId ?? Guid.Empty), cancellationToken)).ToHttpResult();

    private static async Task<IResult> ResetTwoFactor(ResetBody body, HttpContext http, ICurrentUser current, IHandler<ResetStaffTwoFactorRequest, Result> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new ResetStaffTwoFactorRequest(current.UserId ?? Guid.Empty, Ip(http), body.UserId, body.Reason), cancellationToken)).ToHttpResult();

    private static async Task<IResult> Dashboard(ICurrentUser current, IHandler<GetAdminDashboardRequest, Result<GetAdminDashboardResponse>> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new GetAdminDashboardRequest(current.UserId ?? Guid.Empty), cancellationToken)).ToHttpResult();

    private static async Task<IResult> Complaint(Guid id, ICurrentUser current, IHandler<GetComplaintContextRequest, Result<GetComplaintContextResponse>> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new GetComplaintContextRequest(current.UserId ?? Guid.Empty, id), cancellationToken)).ToHttpResult();

    private static async Task<IResult> Users(string? query, ICurrentUser current, IHandler<SearchAdminUsersRequest, Result<SearchAdminUsersResponse>> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new SearchAdminUsersRequest(current.UserId ?? Guid.Empty, query ?? ""), cancellationToken)).ToHttpResult();

    private static async Task<IResult> User(Guid id, bool? revealEmail, bool? revealPhone, HttpContext http, ICurrentUser current, IHandler<GetAdminUserRequest, Result<AdminUserDetail>> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new GetAdminUserRequest(current.UserId ?? Guid.Empty, Ip(http), id, revealEmail == true, revealPhone == true), cancellationToken)).ToHttpResult();

    private static async Task<IResult> Sanction(Guid id, string action, ReasonBody body, HttpContext http, ICurrentUser current, IHandler<SanctionUserRequest, Result> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new SanctionUserRequest(current.UserId ?? Guid.Empty, Ip(http), id, action, body.Reason, body.Until), cancellationToken)).ToHttpResult();

    private static async Task<IResult> Categories(ICurrentUser current, IHandler<ListAdminCategoriesRequest, Result<ListAdminCategoriesResponse>> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new ListAdminCategoriesRequest(current.UserId ?? Guid.Empty), cancellationToken)).ToHttpResult();

    private static async Task<IResult> Category(Guid id, ICurrentUser current, IHandler<GetAdminCategoryRequest, Result<AdminCategoryView>> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new GetAdminCategoryRequest(current.UserId ?? Guid.Empty, id), cancellationToken)).ToHttpResult();

    private static async Task<IResult> CreateCategory(CreateReportCategoryRequest body, HttpContext http, ICurrentUser current, IHandler<CreateReportCategoryRequest, Result<Guid>> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(body with { ActorId = current.UserId ?? Guid.Empty, IpAddress = Ip(http) }, cancellationToken)).ToHttpResult();

    private static async Task<IResult> UpdateCategory(Guid id, UpdateReportCategoryRequest body, HttpContext http, ICurrentUser current, IHandler<UpdateReportCategoryRequest, Result> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(body with { ActorId = current.UserId ?? Guid.Empty, IpAddress = Ip(http), CategoryId = id }, cancellationToken)).ToHttpResult();

    private static async Task<IResult> SetActive(Guid id, ActiveBody body, HttpContext http, ICurrentUser current, IHandler<SetReportCategoryActiveRequest, Result> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new SetReportCategoryActiveRequest(current.UserId ?? Guid.Empty, Ip(http), id, body.IsActive), cancellationToken)).ToHttpResult();

    private static async Task<IResult> Reorder(ReorderBody body, HttpContext http, ICurrentUser current, IHandler<ReorderReportCategoriesRequest, Result> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new ReorderReportCategoriesRequest(current.UserId ?? Guid.Empty, Ip(http), body.CategoryIds), cancellationToken)).ToHttpResult();

    private static async Task<IResult> Override(Guid id, UpsertReportCategoryOverrideRequest body, HttpContext http, ICurrentUser current, IHandler<UpsertReportCategoryOverrideRequest, Result<Guid>> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(body with { ActorId = current.UserId ?? Guid.Empty, IpAddress = Ip(http), CategoryId = id }, cancellationToken)).ToHttpResult();

    private static async Task<IResult> Translation(Guid id, UpsertReportCategoryTranslationRequest body, HttpContext http, ICurrentUser current, IHandler<UpsertReportCategoryTranslationRequest, Result<Guid>> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(body with { ActorId = current.UserId ?? Guid.Empty, IpAddress = Ip(http), CategoryId = id }, cancellationToken)).ToHttpResult();

    private static async Task<IResult> Icons(Guid id, HttpContext http, ICurrentUser current, IHandler<UploadCategoryIconsRequest, Result> handler, CancellationToken cancellationToken)
    {
        var form = await http.Request.ReadFormAsync(cancellationToken);
        var button = await ReadAsync(form.Files.GetFile("button"), UploadCategoryIconsHandler.MaxBytes, cancellationToken);
        var map = await ReadAsync(form.Files.GetFile("map"), UploadCategoryIconsHandler.MaxBytes, cancellationToken);
        return (await handler.Handle(new UploadCategoryIconsRequest(current.UserId ?? Guid.Empty, Ip(http), id, button, map), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> Avatars(ICurrentUser current, IHandler<ListAdminAvatarsRequest, Result<ListAdminAvatarsResponse>> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new ListAdminAvatarsRequest(current.UserId ?? Guid.Empty), cancellationToken)).ToHttpResult();

    private static async Task<IResult> SaveAvatar(Guid? id, HttpContext http, ICurrentUser current, IHandler<SaveAdminAvatarRequest, Result<Guid>> handler, CancellationToken cancellationToken)
    {
        var form = await http.Request.ReadFormAsync(cancellationToken);
        var image = await ReadAsync(form.Files.GetFile("file"), 5 * 1024 * 1024, cancellationToken);
        var active = !string.Equals(form["isActive"], "false", StringComparison.OrdinalIgnoreCase);
        var sort = int.TryParse(form["sortOrder"], out var parsed) ? parsed : 0;
        return (await handler.Handle(new SaveAdminAvatarRequest(current.UserId ?? Guid.Empty, Ip(http), id, form["name"].ToString(), sort, active, image), cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> AvatarActive(Guid id, ActiveBody body, HttpContext http, ICurrentUser current, IHandler<SetAdminAvatarActiveRequest, Result> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new SetAdminAvatarActiveRequest(current.UserId ?? Guid.Empty, Ip(http), id, body.IsActive), cancellationToken)).ToHttpResult();

    private static async Task<IResult> Plans(ICurrentUser current, IHandler<ListAdminPlansRequest, Result<ListAdminPlansResponse>> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new ListAdminPlansRequest(current.UserId ?? Guid.Empty), cancellationToken)).ToHttpResult();

    private static async Task<IResult> Settings(ICurrentUser current, IHandler<ListSettingsRequest, Result<ListSettingsResponse>> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new ListSettingsRequest(current.UserId ?? Guid.Empty), cancellationToken)).ToHttpResult();

    private static async Task<IResult> UpdateSetting(string key, SettingBody body, HttpContext http, ICurrentUser current, IHandler<UpdateSettingRequest, Result> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new UpdateSettingRequest(current.UserId ?? Guid.Empty, Ip(http), Uri.UnescapeDataString(key), body.Value), cancellationToken)).ToHttpResult();

    private static async Task<IResult> MinimumAge(Guid id, MinimumAgeBody body, HttpContext http, ICurrentUser current, IHandler<ChangeCountryMinimumAgeRequest, Result> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new ChangeCountryMinimumAgeRequest(current.UserId ?? Guid.Empty, Ip(http), id, body.MinimumAge), cancellationToken)).ToHttpResult();

    private static async Task<IResult> Holds(bool? activeOnly, ICurrentUser current, IHandler<ListLegalHoldsRequest, Result<ListLegalHoldsResponse>> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new ListLegalHoldsRequest(current.UserId ?? Guid.Empty, activeOnly != false), cancellationToken)).ToHttpResult();

    private static async Task<IResult> PlaceHold(PlaceHoldBody body, HttpContext http, ICurrentUser current, IHandler<PlaceLegalHoldRequest, Result<Guid>> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new PlaceLegalHoldRequest(current.UserId ?? Guid.Empty, Ip(http), body.TargetType, body.TargetId, body.Reason), cancellationToken)).ToHttpResult();

    private static async Task<IResult> ReleaseHold(Guid id, ResolutionBody body, HttpContext http, ICurrentUser current, IHandler<ReleaseLegalHoldRequest, Result> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new ReleaseLegalHoldRequest(current.UserId ?? Guid.Empty, Ip(http), id, body.Resolution ?? ""), cancellationToken)).ToHttpResult();

    private static async Task<IResult> Retention(ICurrentUser current, IHandler<ListRetentionPoliciesRequest, Result<ListRetentionPoliciesResponse>> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new ListRetentionPoliciesRequest(current.UserId ?? Guid.Empty), cancellationToken)).ToHttpResult();

    private static async Task<IResult> UpdateRetention(string kind, RetentionBody body, HttpContext http, ICurrentUser current, IHandler<UpdateRetentionPolicyRequest, Result> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new UpdateRetentionPolicyRequest(current.UserId ?? Guid.Empty, Ip(http), kind, body.RetainDays), cancellationToken)).ToHttpResult();

    private static async Task<IResult> Takedowns(string? status, bool? overdue, string? cursor, int? limit, ICurrentUser current, IHandler<ListTakedownsRequest, Result<ListTakedownsResponse>> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new ListTakedownsRequest(current.UserId ?? Guid.Empty, status, overdue, cursor, limit ?? 20), cancellationToken)).ToHttpResult();

    private static async Task<IResult> Review(Guid id, string action, string? resolution, HttpContext http, ICurrentUser current, IHandler<ReviewTakedownRequest, Result> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new ReviewTakedownRequest(current.UserId ?? Guid.Empty, Ip(http), id, action, resolution), cancellationToken)).ToHttpResult();

    private static async Task<IResult> Audit(Guid? actorId, string? action, string? entityType, DateTimeOffset? from, DateTimeOffset? to, string? cursor, int? limit, ICurrentUser current, IHandler<ListAuditLogsRequest, Result<ListAuditLogsResponse>> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new ListAuditLogsRequest(current.UserId ?? Guid.Empty, actorId, action, entityType, from, to, cursor, limit ?? 50), cancellationToken)).ToHttpResult();

    private static async Task<IResult> AnonymousLogs(string? cursor, int? limit, ICurrentUser current, IHandler<ListAnonymousAccessLogsRequest, Result<ListAnonymousAccessLogsResponse>> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new ListAnonymousAccessLogsRequest(current.UserId ?? Guid.Empty, cursor, limit ?? 50), cancellationToken)).ToHttpResult();

    private static async Task<IResult> Staff(ICurrentUser current, IHandler<ListStaffRequest, Result<ListStaffResponse>> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new ListStaffRequest(current.UserId ?? Guid.Empty), cancellationToken)).ToHttpResult();

    private static async Task<IResult> Assign(AssignBody body, HttpContext http, ICurrentUser current, IHandler<AssignStaffRoleRequest, Result<GrantStaffRoleResponse>> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new AssignStaffRoleRequest(current.UserId ?? Guid.Empty, Ip(http), body.Email, body.Role), cancellationToken)).ToHttpResult();

    private static async Task<IResult> Revoke(Guid id, HttpContext http, ICurrentUser current, IHandler<RevokeStaffRoleRequest, Result> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new RevokeStaffRoleRequest(current.UserId ?? Guid.Empty, Ip(http), id), cancellationToken)).ToHttpResult();

    private static async Task<byte[]> ReadAsync(IFormFile? file, int maxBytes, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0 || file.Length > maxBytes)
        {
            return [];
        }

        using var stream = file.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        return memory.ToArray();
    }

    private static string? Ip(HttpContext httpContext) => httpContext.Connection.RemoteIpAddress?.ToString();
}

public sealed record ReasonBody(string? Reason, DateTimeOffset? Until = null);

public sealed record SuspendBody(string? Reason, DateTimeOffset? Until);

public sealed record ActiveBody(bool IsActive);

public sealed record ReorderBody(IReadOnlyList<Guid> CategoryIds);

public sealed record SettingBody(string Value);

public sealed record MinimumAgeBody(int MinimumAge);

public sealed record PlaceHoldBody(string TargetType, Guid TargetId, string Reason);

public sealed record ResolutionBody(string? Resolution);

public sealed record RetentionBody(int RetainDays);

public sealed record AssignBody(string Email, string Role);

public sealed record ResetBody(Guid UserId, string Reason);

public sealed class AdminAuthEndpoints : IEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        var auth = group.MapGroup("/admin/auth").AllowAnonymous().WithTags("Admin");
        auth.MapPost("/login", Login).WithName("AdminLogin").Produces<AdminLoginResponse>().RequireRateLimiting("admin-login");
        auth.MapPost("/mfa", Mfa).WithName("VerifyStaffMfa").Produces<StaffSessionResponse>().RequireRateLimiting("admin-mfa");
        auth.MapPost("/refresh", Refresh).WithName("RefreshStaffSession").Produces<StaffSessionResponse>().RequireRateLimiting("admin-login");
        auth.MapPost("/logout", Logout).WithName("LogoutStaffSession").Produces(StatusCodes.Status204NoContent).RequireRateLimiting("admin-login");

        var enroll = group.MapGroup("/admin/2fa")
            .RequireAuthorization(AuthorizationPolicies.MfaEnrollment)
            .RequireRateLimiting("admin-mfa")
            .WithTags("Admin");
        enroll.MapPost("/setup", Setup).WithName("SetupStaffTwoFactor").Produces<SetupStaffTwoFactorResponse>();
        enroll.MapPost("/enable", Enable).WithName("EnableStaffTwoFactor").Produces<EnableStaffTwoFactorResponse>();
    }

    private static async Task<IResult> Login(LoginBody body, HttpContext http, IHandler<AdminLoginRequest, Result<AdminLoginResponse>> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new AdminLoginRequest(body.Email, body.Password, Ip(http), http.Request.Headers.UserAgent.ToString()), cancellationToken)).ToHttpResult();

    private static async Task<IResult> Mfa(MfaBody body, HttpContext http, IHandler<VerifyStaffMfaRequest, Result<StaffSessionResponse>> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new VerifyStaffMfaRequest(body.MfaToken, body.Code, body.RecoveryCode, Ip(http), http.Request.Headers.UserAgent.ToString()), cancellationToken)).ToHttpResult();

    private static async Task<IResult> Refresh(RefreshBody body, HttpContext http, IHandler<RefreshStaffSessionRequest, Result<StaffSessionResponse>> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new RefreshStaffSessionRequest(body.RefreshToken, Ip(http)), cancellationToken)).ToHttpResult();

    private static async Task<IResult> Logout(RefreshBody body, HttpContext http, IHandler<LogoutStaffSessionRequest, Result> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new LogoutStaffSessionRequest(body.RefreshToken, Ip(http)), cancellationToken)).ToHttpResult();

    private static async Task<IResult> Setup(HttpContext http, ICurrentUser current, IHandler<SetupStaffTwoFactorRequest, Result<SetupStaffTwoFactorResponse>> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new SetupStaffTwoFactorRequest(current.UserId ?? Guid.Empty, Ip(http)), cancellationToken)).ToHttpResult();

    private static async Task<IResult> Enable(EnableBody body, HttpContext http, ICurrentUser current, IHandler<EnableStaffTwoFactorRequest, Result<EnableStaffTwoFactorResponse>> handler, CancellationToken cancellationToken) =>
        (await handler.Handle(new EnableStaffTwoFactorRequest(current.UserId ?? Guid.Empty, body.Code, Ip(http), http.Request.Headers.UserAgent.ToString()), cancellationToken)).ToHttpResult();

    private static string? Ip(HttpContext httpContext) => httpContext.Connection.RemoteIpAddress?.ToString();

    private sealed record LoginBody(string Email, string Password);
    private sealed record MfaBody(string MfaToken, string? Code, string? RecoveryCode);
    private sealed record RefreshBody(string RefreshToken);
    private sealed record EnableBody(string Code);
}

public sealed class TakedownEndpoints : IEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/takedown-requests", Submit)
            .WithName("SubmitTakedownRequest")
            .WithTags("Takedowns")
            .WithSummary("Formulario público para pedir el retiro de un contenido. La respuesta no dice si el contenido existe.")
            .RequireRateLimiting("takedown-public")
            .AllowAnonymous();
    }

    private static async Task<IResult> Submit(
        TakedownBody body,
        HttpContext http,
        IHandler<SubmitTakedownRequest, Result<SubmitTakedownResponse>> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new SubmitTakedownRequest(
            body.RequesterName,
            body.RequesterEmail,
            body.TargetType,
            body.TargetId,
            body.Description,
            body.Website,
            body.ElapsedMs,
            http.Connection.RemoteIpAddress?.ToString(),
            body.CaptchaToken), cancellationToken);
        return result.IsSuccess ? Results.Accepted(value: result.Value) : result.ToHttpResult();
    }
}

public sealed record TakedownBody(string RequesterName, string RequesterEmail, string TargetType, Guid TargetId, string Description, string? Website, int? ElapsedMs, string? CaptchaToken);
