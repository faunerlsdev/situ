using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Administration.Catalog;
using Situ.Application.Features.Administration.Complaints;
using Situ.Application.Features.Administration.GetAdminSession;
using Situ.Application.Features.Administration.Settings;
using Situ.Application.Features.Administration.Staff;
using Situ.Application.Features.Administration.Takedowns;
using Situ.Application.Features.Administration.Users;
using Situ.Application.Features.Configuration.GetAppConfig;
using Situ.Application.Tests.Support;
using Situ.Domain.Geography;
using Situ.Domain.Administration;
using Situ.Domain.Common;
using Situ.Domain.Identity;
using Situ.Domain.Moderation;
using Situ.Domain.Reports;
using Situ.Infrastructure.Reports;

namespace Situ.Application.Tests.Administration;

public class AdminPanelFlowTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Endpoints_reject_a_missing_session_a_normal_user_and_staff_without_the_permission()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var person = Person(db, "ana@correo.com", "ana", new DateOnly(1996, 4, 4));
        var staff = Person(db, "mod@correo.com", "modera", new DateOnly(1990, 1, 1));
        await db.SaveChangesAsync();
        await Grant(db, staff.Id, StaffPermissionCodes.UsersView);

        var dashboard = new GetAdminDashboardHandler(db, clock);
        Assert.Equal("admin.unauthenticated", (await dashboard.Handle(new GetAdminDashboardRequest(Guid.Empty), CancellationToken.None)).Error!.Code);
        Assert.Equal("admin.forbidden", (await dashboard.Handle(new GetAdminDashboardRequest(person.Id), CancellationToken.None)).Error!.Code);
        Assert.Equal("admin.forbidden", (await dashboard.Handle(new GetAdminDashboardRequest(staff.Id), CancellationToken.None)).Error!.Code);

        var search = new SearchAdminUsersHandler(db, clock);
        Assert.Equal("admin.forbidden", (await search.Handle(new SearchAdminUsersRequest(person.Id, "ana"), CancellationToken.None)).Error!.Code);
        Assert.Equal("ana", (await search.Handle(new SearchAdminUsersRequest(staff.Id, "ana"), CancellationToken.None)).Value!.Users.Single().UserName);

        var settings = new UpdateSettingHandler(db, clock);
        Assert.Equal("admin.forbidden", (await settings.Handle(new UpdateSettingRequest(staff.Id, "127.0.0.1", SystemSettingKeys.LoginMaxFailedAttempts, "4"), CancellationToken.None)).Error!.Code);
    }

    [Fact]
    public async Task Console_grant_assigns_an_existing_user_and_records_the_audit()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var role = StaffRole.Create("Super admin", "Acceso total.", true, Now);
        db.StaffRoles.Add(role);
        var user = Person(db, "fauner@correo.com", "fauner", new DateOnly(1992, 3, 2));
        await db.SaveChangesAsync();

        var missing = await new GrantStaffRoleHandler(db, clock).Handle(new GrantStaffRoleRequest(null, "nadie@correo.com", "SuperAdmin", null), CancellationToken.None);
        Assert.Equal("staff.user_not_found", missing.Error!.Code);
        var failed = await db.AuditLogs.SingleAsync();
        Assert.Equal("staff.grant_failed", failed.Action);
        Assert.DoesNotContain("nadie@correo.com", failed.Changes);

        var granted = await new GrantStaffRoleHandler(db, clock).Handle(new GrantStaffRoleRequest(null, "fauner@correo.com", "SuperAdmin", null), CancellationToken.None);
        Assert.Equal(role.Name, granted.Value!.RoleName);
        var again = await new GrantStaffRoleHandler(db, clock).Handle(new GrantStaffRoleRequest(null, "FAUNER@correo.com", "super-admin", null), CancellationToken.None);
        Assert.Equal(granted.Value.AssignmentId, again.Value!.AssignmentId);
        Assert.Equal(1, await db.StaffAssignments.CountAsync());
        Assert.Contains(db.AuditLogs, item => item.Action == "staff.grant" && item.Changes!.Contains(user.Id.ToString()) && !item.Changes.Contains("fauner@correo.com"));

        var revoked = await new RevokeStaffRoleHandler(db, clock).Handle(new RevokeStaffRoleRequest(user.Id, null, granted.Value.AssignmentId), CancellationToken.None);
        Assert.Equal("admin.forbidden", revoked.Error!.Code);
        await Grant(db, user.Id, StaffPermissionCodes.StaffManage);
        var last = await new RevokeStaffRoleHandler(db, clock).Handle(new RevokeStaffRoleRequest(user.Id, null, granted.Value.AssignmentId), CancellationToken.None);
        Assert.Equal("staff.last_super_admin", last.Error!.Code);
        Assert.True((await db.StaffAssignments.SingleAsync(item => item.Id == granted.Value.AssignmentId)).IsActive);
    }

    [Fact]
    public async Task Sanctions_follow_the_domain_and_a_minor_cannot_publish_while_suspended()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var staff = Person(db, "mod@correo.com", "modera", new DateOnly(1990, 1, 1));
        var adult = Person(db, "ana@correo.com", "ana", new DateOnly(1996, 4, 4));
        var minor = Person(db, "salma@correo.com", "salma", new DateOnly(2010, 1, 1));
        minor.ConfirmEmail(Now);
        minor.GrantGuardianConsent(Now);
        Assert.True(minor.CanPublish);
        var device = UserDevice.Register(adult.Id, "telefono", DevicePlatform.Android, null, null, null, Now);
        db.UserDevices.Add(device);
        db.RefreshTokens.Add(RefreshToken.Issue(adult.Id, device.Id, "hash", Now, TimeSpan.FromDays(1)));
        await db.SaveChangesAsync();
        await Grant(db, staff.Id, StaffPermissionCodes.UsersSuspend);

        var handler = new SanctionUserHandler(db, clock);
        Assert.Equal("admin.forbidden", (await handler.Handle(new SanctionUserRequest(adult.Id, "10.0.0.1", adult.Id, "warn", "motivo", null), CancellationToken.None)).Error!.Code);

        await handler.Handle(new SanctionUserRequest(staff.Id, "10.0.0.1", adult.Id, "warn", "aviso", null), CancellationToken.None);
        Assert.Equal(UserStatus.Active, (await db.Users.SingleAsync(item => item.Id == adult.Id)).Status);
        await handler.Handle(new SanctionUserRequest(staff.Id, "10.0.0.1", adult.Id, "suspend", "pausa", Now.AddDays(2)), CancellationToken.None);
        Assert.False((await db.Users.SingleAsync(item => item.Id == adult.Id)).CanPublish);
        await handler.Handle(new SanctionUserRequest(staff.Id, "10.0.0.1", adult.Id, "ban", "cierre", null), CancellationToken.None);
        Assert.Equal(UserStatus.Banned, (await db.Users.SingleAsync(item => item.Id == adult.Id)).Status);
        await handler.Handle(new SanctionUserRequest(staff.Id, "10.0.0.1", adult.Id, "reinstate", "vuelve", null), CancellationToken.None);
        Assert.True((await db.Users.SingleAsync(item => item.Id == adult.Id)).CanPublish);
        await handler.Handle(new SanctionUserRequest(staff.Id, "10.0.0.1", adult.Id, "force-logout", "remoto", null), CancellationToken.None);
        Assert.NotNull((await db.UserDevices.SingleAsync()).RevokedAt);
        Assert.NotNull((await db.RefreshTokens.SingleAsync()).RevokedAt);

        await handler.Handle(new SanctionUserRequest(staff.Id, "10.0.0.1", minor.Id, "suspend", "menor", Now.AddDays(1)), CancellationToken.None);
        Assert.False((await db.Users.SingleAsync(item => item.Id == minor.Id)).CanPublish);
        var audit = await db.AuditLogs.SingleAsync(item => item.Action == "user.suspenduser" && item.EntityId == adult.Id.ToString());
        Assert.Contains("Active", audit.Changes);
        Assert.Contains("Suspended", audit.Changes);
        Assert.DoesNotContain("ana@correo.com", audit.Changes);
        Assert.Equal(6, await db.ModerationActions.CountAsync());
    }

    [Fact]
    public async Task Anonymous_complaint_context_hides_the_author()
    {
        await using var db = IdentityTestDb.Create();
        var author = Person(db, "autor@correo.com", "autoranon", new DateOnly(1994, 2, 2));
        var reporter = Person(db, "reporte@correo.com", "reporte", new DateOnly(1993, 2, 2));
        var staff = Person(db, "mod@correo.com", "modera", new DateOnly(1990, 1, 1));
        var category = ReportCategory.Create("evento", "Evento", "Descripción de la categoría de prueba.", "/a.png", "/b.png", "#112233", 1, 90, 5);
        db.ReportCategories.Add(category);
        var report = Report.Create(author.Id, category.Id, null, new GeoPoint(4.711, -74.072), "Un hecho en la esquina sin decir quién.", true, false, TimeSpan.FromHours(2), Now);
        db.Reports.Add(report);
        var complaint = Complaint.File(reporter.Id, ContentTargetType.Report, report.Id, ComplaintReason.Spam, null, Now);
        db.Complaints.Add(complaint);
        await db.SaveChangesAsync();
        await Grant(db, staff.Id, StaffPermissionCodes.ComplaintsResolve);

        var context = await new GetComplaintContextHandler(db).Handle(new GetComplaintContextRequest(staff.Id, complaint.Id), CancellationToken.None);
        Assert.True(context.Value!.Target.AuthorHidden);
        Assert.Null(context.Value.Target.Author);
        Assert.Empty(context.Value.Sanctions);
        var json = JsonSerializer.Serialize(context.Value);
        Assert.DoesNotContain(author.Id.ToString(), json);
        Assert.DoesNotContain("autoranon", json);
        Assert.DoesNotContain("autor@correo.com", json);
        Assert.DoesNotContain("1994", json);
    }

    [Fact]
    public async Task User_detail_masks_email_hides_birth_date_and_logs_a_full_read()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var person = Person(db, "ana@correo.com", "ana", new DateOnly(1996, 4, 4));
        var staff = Person(db, "mod@correo.com", "modera", new DateOnly(1990, 1, 1));
        var category = ReportCategory.Create("evento", "Evento", "Descripción de la categoría de prueba.", "/a.png", "/b.png", "#112233", 1, 90, 5);
        db.ReportCategories.Add(category);
        var hidden = Report.Create(person.Id, category.Id, null, new GeoPoint(4.7, -74.0), "Esto es anónimo y no sale en la ficha.", true, false, TimeSpan.FromHours(1), Now);
        db.Reports.Add(hidden);
        await db.SaveChangesAsync();
        await Grant(db, staff.Id, StaffPermissionCodes.UsersView);

        var handler = new GetAdminUserHandler(db, clock);
        var masked = await handler.Handle(new GetAdminUserRequest(staff.Id, "10.0.0.8", person.Id, true), CancellationToken.None);
        Assert.Equal("a***@correo.com", masked.Value!.Email);
        Assert.False(masked.Value.EmailRevealed);
        var maskedJson = JsonSerializer.Serialize(masked.Value);
        Assert.DoesNotContain("ana@correo.com", maskedJson);
        Assert.DoesNotContain("dateOfBirth", maskedJson);
        Assert.DoesNotContain("1996-04-04", maskedJson);
        Assert.DoesNotContain(hidden.Id.ToString(), maskedJson);
        Assert.Empty(db.AuditLogs);

        await Grant(db, staff.Id, StaffPermissionCodes.UsersViewEmail);
        var revealed = await handler.Handle(new GetAdminUserRequest(staff.Id, "10.0.0.8", person.Id, true), CancellationToken.None);
        Assert.Equal("ana@correo.com", revealed.Value!.Email);
        var audit = await db.AuditLogs.SingleAsync();
        Assert.Equal("users.view_email", audit.Action);
        Assert.DoesNotContain("ana@correo.com", audit.Changes);
    }

    [Fact]
    public async Task Settings_validate_each_key_hide_secrets_and_the_next_app_config_read_sees_the_change()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var staff = Person(db, "mod@correo.com", "modera", new DateOnly(1990, 1, 1));
        db.SystemSettings.Add(SystemSetting.Create("smtp.password", "no-mostrar", "Secreto.", Now));
        db.SystemSettings.Add(SystemSetting.Create(SystemSettingKeys.PanelBrowseCopy, "Quiero chismosear", "Texto.", Now));
        await db.SaveChangesAsync();
        await Grant(db, staff.Id, StaffPermissionCodes.SettingsManage);

        var list = await new ListSettingsHandler(db).Handle(new ListSettingsRequest(staff.Id), CancellationToken.None);
        Assert.DoesNotContain(list.Value!.Groups.SelectMany(group => group.Items), item => item.Key == "smtp.password");
        var update = new UpdateSettingHandler(db, clock);
        Assert.Equal("settings.not_editable", (await update.Handle(new UpdateSettingRequest(staff.Id, "10.0.0.1", "smtp.password", "otro"), CancellationToken.None)).Error!.Code);
        Assert.Equal("settings.invalid_value", (await update.Handle(new UpdateSettingRequest(staff.Id, "10.0.0.1", SystemSettingKeys.LoginMaxFailedAttempts, "2"), CancellationToken.None)).Error!.Code);
        Assert.Equal("settings.invalid_value", (await update.Handle(new UpdateSettingRequest(staff.Id, "10.0.0.1", SystemSettingKeys.LoginLockoutMinutes, "4"), CancellationToken.None)).Error!.Code);
        Assert.Equal("settings.invalid_value", (await update.Handle(new UpdateSettingRequest(staff.Id, "10.0.0.1", SystemSettingKeys.DefaultNotificationRadiusMeters, "50"), CancellationToken.None)).Error!.Code);
        Assert.True((await update.Handle(new UpdateSettingRequest(staff.Id, "10.0.0.1", SystemSettingKeys.LoginMaxFailedAttempts, "4"), CancellationToken.None)).IsSuccess);
        Assert.True((await update.Handle(new UpdateSettingRequest(staff.Id, "10.0.0.1", SystemSettingKeys.PanelBrowseCopy, "Mirar el mapa"), CancellationToken.None)).IsSuccess);
        var copies = """[{"countryCode":"CO","locale":"es-CO","browse":"Chismosear","report":"Reportar"}]""";
        Assert.True((await update.Handle(new UpdateSettingRequest(staff.Id, "10.0.0.1", SystemSettingKeys.PanelCopyByCountry, copies), CancellationToken.None)).IsSuccess);

        var config = await new GetAppConfigHandler(db).Handle(new GetAppConfigRequest(), CancellationToken.None);
        Assert.Equal("Mirar el mapa", config.Value!.PanelCopy.Browse);
        Assert.Equal("CO", config.Value.PanelCopyByCountry.Single().CountryCode);
        var audit = await db.AuditLogs.SingleAsync(item => item.EntityId == SystemSettingKeys.LoginMaxFailedAttempts);
        Assert.Contains("\"before\":\"5\"", audit.Changes);
        Assert.Contains("\"after\":\"4\"", audit.Changes);
        Assert.DoesNotContain("no-mostrar", audit.Changes);
    }

    [Fact]
    public async Task Category_and_avatar_uploads_reject_a_bad_file_and_bump_the_catalog()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var files = new MemoryFiles();
        var staff = Person(db, "mod@correo.com", "modera", new DateOnly(1990, 1, 1));
        var category = ReportCategory.Create("evento", "Evento", "Descripción de la categoría de prueba.", "/a.png", "/b.png", "#112233", 1, 90, 5);
        db.ReportCategories.Add(category);
        db.SystemSettings.Add(SystemSetting.Create(SystemSettingKeys.ReportCatalogVersion, "3", "Versión.", Now));
        await db.SaveChangesAsync();
        await Grant(db, staff.Id, StaffPermissionCodes.CategoriesManage);
        await Grant(db, staff.Id, StaffPermissionCodes.AvatarsManage);

        var icons = new UploadCategoryIconsHandler(db, clock, files, new AcceptIcons(false));
        var rejected = await icons.Handle(new UploadCategoryIconsRequest(staff.Id, "10.0.0.1", category.Id, [1], [1]), CancellationToken.None);
        Assert.Equal("icon.invalid_image", rejected.Error!.Code);
        Assert.Equal("3", (await db.SystemSettings.SingleAsync(item => item.Key == SystemSettingKeys.ReportCatalogVersion)).Value);

        var accepted = new UploadCategoryIconsHandler(db, clock, files, new AcceptIcons(true));
        Assert.True((await accepted.Handle(new UploadCategoryIconsRequest(staff.Id, "10.0.0.1", category.Id, [9], [9]), CancellationToken.None)).IsSuccess);
        Assert.Equal("4", (await db.SystemSettings.SingleAsync(item => item.Key == SystemSettingKeys.ReportCatalogVersion)).Value);
        Assert.StartsWith("/uploads/categories/", (await db.ReportCategories.SingleAsync()).ButtonIconUrl);

        var avatars = new SaveAdminAvatarHandler(db, clock, files, new AcceptPhotos(false));
        Assert.Equal("avatar.invalid_image", (await avatars.Handle(new SaveAdminAvatarRequest(staff.Id, "10.0.0.1", null, "Pin", 1, true, [1]), CancellationToken.None)).Error!.Code);
        var saved = await new SaveAdminAvatarHandler(db, clock, files, new AcceptPhotos(true)).Handle(new SaveAdminAvatarRequest(staff.Id, "10.0.0.1", null, "Pin", 4, true, [1]), CancellationToken.None);
        Assert.True(saved.IsSuccess);
        Assert.Contains(files.Keys, key => key.StartsWith("public/avatars/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Public_takedown_is_neutral_rate_limited_and_due_in_fifteen_days()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var mail = new FakeEmailSender();
        var handler = new SubmitTakedownHandler(db, clock, mail);
        var honeypot = await handler.Handle(Request("bot@correo.com", "http://spam.test"), CancellationToken.None);
        Assert.True(honeypot.Value!.Accepted);
        Assert.Empty(db.TakedownRequests);

        for (var index = 0; index < 3; index++)
        {
            var sent = await handler.Handle(Request("persona@correo.com", null), CancellationToken.None);
            Assert.StartsWith("TD-", sent.Value!.TrackingCode);
        }

        var stored = await db.TakedownRequests.FirstAsync();
        Assert.Equal(Now.Add(TakedownRequest.DefaultResponseWindow), stored.DueAt);
        Assert.Contains(stored.Id.ToString("N")[..8], mail.Messages[0].Body, StringComparison.OrdinalIgnoreCase);
        var limited = await handler.Handle(Request("persona@correo.com", null), CancellationToken.None);
        Assert.Equal("takedown.rate_limited", limited.Error!.Code);
        Assert.Equal(3, await db.TakedownRequests.CountAsync());
    }

    [Fact]
    public void Staff_session_is_shorter_than_a_user_session()
    {
        Assert.Equal(TimeSpan.FromMinutes(5), SessionPolicy.AccessLifetimeFor("Staff"));
        Assert.Equal(TimeSpan.FromMinutes(30), SessionPolicy.RefreshLifetimeFor("Staff"));
        Assert.Equal(TimeSpan.FromMinutes(15), SessionPolicy.AccessLifetimeFor(null));
        Assert.Equal(TimeSpan.FromDays(180), SessionPolicy.RefreshLifetimeFor(null));
        Assert.True(SessionPolicy.AccessLifetimeFor("Staff") < SessionPolicy.AccessLifetimeFor(null));
    }

    [Fact]
    public void Icon_processor_accepts_a_real_png_and_rejects_bytes_that_only_look_like_one()
    {
        var processor = new SkiaAdminIconProcessor();
        Assert.Equal("icon.invalid_image", processor.Process("not-an-image"u8.ToArray(), 1000, 32, 1024).Error!.Code);
        using var bitmap = new SkiaSharp.SKBitmap(64, 64);
        bitmap.Erase(SkiaSharp.SKColors.Red);
        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        var png = data.ToArray();
        Assert.True(processor.Process(png, 1024 * 1024, 32, 1024).IsSuccess);
        Assert.Equal("icon.invalid_dimensions", processor.Process(png, 1024 * 1024, 128, 1024).Error!.Code);
    }

    [Fact]
    public async Task Category_catalog_returns_ttl_status_order_weight_and_the_reports_active_right_now()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var staff = Person(db, "gestor@correo.com", "gestor", new DateOnly(1990, 1, 1));
        var category = ReportCategory.Create("hurto", "Hurto", "Robo en la vía.", "/b.png", "/m.png", "#112233", 4, 90, 2.5);
        db.ReportCategories.Add(category);
        var live = Report.Create(staff.Id, category.Id, null, new GeoPoint(4.71, -74.07), "Sigue en la esquina.", false, false, TimeSpan.FromHours(2), Now);
        var gone = Report.Create(staff.Id, category.Id, null, new GeoPoint(4.71, -74.07), "Esto ya venció.", false, false, TimeSpan.FromHours(2), Now);
        gone.Expire(Now);
        db.Reports.AddRange(live, gone);
        await db.SaveChangesAsync();
        await Grant(db, staff.Id, StaffPermissionCodes.CategoriesManage);

        var list = await new ListAdminCategoriesHandler(db, clock).Handle(new ListAdminCategoriesRequest(staff.Id), CancellationToken.None);
        var row = Assert.Single(list.Value!.Categories);
        Assert.Equal(90, row.TtlMinutes);
        Assert.Equal(2.5, row.ImportanceWeight);
        Assert.True(row.IsActive);
        Assert.Equal(4, row.SortOrder);
        Assert.Equal(1, row.ActiveReportCount);

        var json = JsonSerializer.Serialize(list.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("\"ttlMinutes\":90", json);
        Assert.Contains("\"importanceWeight\":2.5", json);
        Assert.Contains("\"isActive\":true", json);
        Assert.Contains("\"sortOrder\":4", json);
        Assert.Contains("\"activeReportCount\":1", json);

        var one = await new GetAdminCategoryHandler(db, clock).Handle(new GetAdminCategoryRequest(staff.Id, category.Id), CancellationToken.None);
        Assert.Equal(90, one.Value!.TtlMinutes);
        Assert.Equal(1, one.Value.ActiveReportCount);
        var missing = await new GetAdminCategoryHandler(db, clock).Handle(new GetAdminCategoryRequest(staff.Id, Guid.CreateVersion7()), CancellationToken.None);
        Assert.Equal("category.not_found", missing.Error!.Code);
    }

    private static SubmitTakedownRequest Request(string email, string? website) =>
        new("Ana Pérez", email, "Report", Guid.CreateVersion7(), "Aparezco en este reporte y pido el retiro.", website, 3000, "10.1.1.1");

    private static User Person(IdentityTestDb db, string email, string userName, DateOnly birth)
    {
        var country = db.Countries.Local.FirstOrDefault();
        var region = db.Regions.Local.FirstOrDefault();
        var city = db.Cities.Local.FirstOrDefault();
        if (country is null || region is null || city is null)
        {
            var created = Country();
            country = created.Country;
            region = created.Region;
            city = created.City;
            db.AddRange(country, region, city);
        }

        var user = User.Register(
            Email.Create(email),
            UserName.Create(userName),
            PersonName.Create("Ana"),
            PersonName.Create("Pérez"),
            PhoneNumber.Create("300555" + (1000 + db.Users.Local.Count).ToString()),
            "hash",
            birth,
            country.Id,
            region.Id,
            city.Id,
            "es-CO",
            14,
            Now);
        user.ConfirmEmail(Now);
        db.Users.Add(user);
        return user;
    }

    private static (Situ.Domain.Geography.Country Country, Situ.Domain.Geography.Region Region, Situ.Domain.Geography.City City) Country()
    {
        var country = Situ.Domain.Geography.Country.Create("CO", "Colombia", "Departamento", "es-CO", "COP", "America/Bogota", DistanceUnit.Kilometers, 14, true);
        var region = Situ.Domain.Geography.Region.Create(country.Id, "Cundinamarca", "25");
        var city = Situ.Domain.Geography.City.Create(region.Id, country.Id, "Bogotá", new GeoPoint(4.711, -74.072), "America/Bogota");
        return (country, region, city);
    }

    private static async Task Grant(IdentityTestDb db, Guid userId, string permission)
    {
        var role = StaffRole.Create(permission, permission, false, Now);
        db.StaffRoles.Add(role);
        db.StaffPermissions.Add(StaffPermission.Grant(role.Id, permission));
        db.StaffAssignments.Add(StaffAssignment.Assign(userId, role.Id, null, Now));
        await db.SaveChangesAsync();
    }

    private sealed class AcceptIcons(bool accept) : IAdminIconProcessor
    {
        public Result<byte[]> Process(byte[] content, int maxBytes, int minEdge, int maxEdge) =>
            accept ? Result<byte[]>.Success([1, 2, 3]) : Result<byte[]>.Failure(Error.Validation("icon.invalid_image", "Sube una imagen JPG, PNG o WebP."));
    }

    private sealed class AcceptPhotos(bool accept) : IAvatarImageProcessor
    {
        public Result<byte[]> Process(byte[] content, int maxBytes) =>
            accept ? Result<byte[]>.Success([1, 2, 3]) : Result<byte[]>.Failure(Error.Validation("avatar.invalid_image", "Sube una imagen JPG, PNG o WebP."));
    }

    private sealed class MemoryFiles : IFileStorage
    {
        public List<string> Keys { get; } = [];

        public Task SaveAsync(string key, byte[] content, CancellationToken cancellationToken)
        {
            Keys.Add(key);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken) => Task.FromResult<Stream?>(null);
    }

}
