using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Features.Configuration.GetAppConfig;
using Situ.Application.Features.Identity.GetSessionProfile;
using Situ.Application.Features.Reports.CreateReport;
using Situ.Application.Features.Reports.RemoveReport;
using Situ.Application.Tests.Support;
using Situ.Domain.Administration;
using Situ.Domain.Common;
using Situ.Domain.Geography;
using Situ.Domain.Guardianship;
using Situ.Domain.Identity;
using Situ.Domain.Moderation;
using Situ.Domain.Monetization;
using Situ.Domain.Reports;

namespace Situ.Application.Tests.Identity;

public class SessionProfileTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Session_profile_hides_the_birth_date_and_reports_publish_blocks()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var adult = await UserAsync(db, clock, 30, "ana@situ.test", "ana", confirm: true, guardian: false);
        var pending = await UserAsync(db, clock, 30, "pending@situ.test", "pending", confirm: false, guardian: false);
        var minor = await UserAsync(db, clock, 16, "lia@situ.test", "lia", confirm: true, guardian: false);
        var suspended = await UserAsync(db, clock, 30, "sus@situ.test", "suspended", confirm: true, guardian: false);
        suspended.Suspend(Now.AddDays(2));
        var banned = await UserAsync(db, clock, 30, "ban@situ.test", "banned", confirm: true, guardian: false);
        banned.Ban();
        await db.SaveChangesAsync();
        await PlanAsync(db, "free", PlanTier.Free, 10, 3, true, 2);

        var adultProfile = await Profile(db, clock, adult.Id);
        var json = JsonSerializer.Serialize(adultProfile, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.DoesNotContain("dateOfBirth", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"age\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(adult.Email.Value, json, StringComparison.OrdinalIgnoreCase);
        Assert.False(adultProfile.IsMinor);
        Assert.True(adultProfile.CanPublish);
        Assert.Null(adultProfile.PublishBlockedReason);
        Assert.Null(adultProfile.Guardian);
        Assert.Equal("free", adultProfile.Plan.Code);
        Assert.Equal(10, adultProfile.Plan.Limits.MaxReportsPerDay);
        Assert.Equal(3, adultProfile.Plan.Limits.MaxAnonymousPostsPerDay);
        Assert.Equal("Bogotá", adultProfile.HomeCity.Name);
        Assert.Equal(4.711, adultProfile.HomeCity.Latitude);
        Assert.Equal("Private", adultProfile.Profile.Visibility);

        var waiting = await Profile(db, clock, pending.Id);
        Assert.False(waiting.CanPublish);
        Assert.Equal("email_not_confirmed", waiting.PublishBlockedReason);
        Assert.False(waiting.EmailConfirmed);

        var child = await Profile(db, clock, minor.Id);
        Assert.True(child.IsMinor);
        Assert.False(child.CanPublish);
        Assert.Equal("guardian_consent_pending", child.PublishBlockedReason);
        Assert.Equal("None", child.Guardian!.Status);
        Assert.False(child.Guardian.Permissions.LiveStreaming);

        db.GuardianConsents.Add(GuardianConsent.Request(minor.Id, "tutor@situ.test", "hash", "v1", Now, Now));
        await db.SaveChangesAsync();
        var withConsent = await Profile(db, clock, minor.Id);
        Assert.Equal("Requested", withConsent.Guardian!.Status);

        Assert.Equal("suspended", (await Profile(db, clock, suspended.Id)).PublishBlockedReason);
        Assert.Equal("banned", (await Profile(db, clock, banned.Id)).PublishBlockedReason);
    }

    [Fact]
    public async Task Session_profile_uses_the_active_subscription_plan()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var user = await UserAsync(db, clock, 30, "ana@situ.test", "ana", true, false);
        var free = await PlanAsync(db, "free", PlanTier.Free, 10, 3, true, 2);
        var pro = await PlanAsync(db, "pro", PlanTier.Pro, 50, 20, false, 5);
        var price = PlanPrice.Create(pro.Id, BillingPeriod.Monthly, "COP", 19900, null);
        price.MarkCreated(Now);
        db.PlanPrices.Add(price);
        db.Subscriptions.Add(Subscription.Start(user.Id, pro.Id, price.Id, BillingPeriod.Monthly, null, Now));
        await db.SaveChangesAsync();
        _ = free;

        var profile = await Profile(db, clock, user.Id);
        Assert.Equal("pro", profile.Plan.Code);
        Assert.Equal("Pro", profile.Plan.Tier);
        Assert.False(profile.Plan.Limits.ShowsAds);
        Assert.Equal(50, profile.Plan.Limits.MaxReportsPerDay);
    }

    [Fact]
    public async Task App_config_follows_system_settings()
    {
        await using var db = IdentityTestDb.Create();
        db.SystemSettings.Add(SystemSetting.Create(SystemSettingKeys.PanelBrowseCopy, "Mira el mapa", "Texto.", Now));
        db.SystemSettings.Add(SystemSetting.Create(SystemSettingKeys.ReportMaxImages, "4", "Imágenes.", Now));
        db.SystemSettings.Add(SystemSetting.Create(SystemSettingKeys.AnonymousDisplayName, "Alguien", "Nombre.", Now));
        await db.SaveChangesAsync();

        var config = await new GetAppConfigHandler(db).Handle(new GetAppConfigRequest(), CancellationToken.None);
        Assert.Equal("Mira el mapa", config.Value!.PanelCopy.Browse);
        Assert.Equal("Quiero reportar algo", config.Value.PanelCopy.Report);
        Assert.Equal(4, config.Value.MediaLimits.MaxImages);
        Assert.Equal(1, config.Value.MediaLimits.MaxVideos);
        Assert.Equal(60, config.Value.MediaLimits.MaxVideoSeconds);
        Assert.Equal(2000, config.Value.ConfirmationMaxDistanceMeters);
        Assert.Equal("Alguien", config.Value.AnonymityDisplayName);
        Assert.Equal("0.1.0", config.Value.Versions.Ios.MinSupported);
    }

    [Fact]
    public async Task Staff_removal_requires_permission_and_writes_the_audit()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var author = await UserAsync(db, clock, 30, "ana@situ.test", "ana", true, false);
        var staff = await UserAsync(db, clock, 30, "mod@situ.test", "mod", true, false);
        var outsider = await UserAsync(db, clock, 30, "out@situ.test", "out", true, false);
        await PlanAsync(db, "free", PlanTier.Free, 10, 3, true, 2);
        var category = ReportCategory.Create("evento", "Evento", "Descripción de la categoría de prueba.", "/assets/categories/evento.png", "/assets/categories/evento.png", "#112233", 1, 90, 5, MediaRequirement.None, isAdultOnly: false);
        category.MarkCreated(Now);
        db.ReportCategories.Add(category);
        await db.SaveChangesAsync();
        var created = await new CreateReportHandler(db, clock, new Signer()).Handle(
            new CreateReportRequest(author.Id, category.Id, "Hay novedad en esta esquina ahora mismo.", 4.711, -74.072, ContentVisibility.Public, false, []),
            CancellationToken.None);
        var reportId = created.Value!.Report.Id;

        var denied = await new RemoveReportHandler(db, clock).Handle(new RemoveReportRequest(outsider.Id, reportId, "No corresponde."), CancellationToken.None);
        Assert.Equal("category.forbidden", denied.Error!.Code);
        Assert.Empty(db.ModerationActions);

        var role = StaffRole.Create("Moderación", "Retira reportes.", true, Now);
        db.StaffRoles.Add(role);
        db.StaffPermissions.Add(StaffPermission.Grant(role.Id, StaffPermissionCodes.ComplaintsResolve));
        db.StaffAssignments.Add(StaffAssignment.Assign(staff.Id, role.Id, null, Now));
        await db.SaveChangesAsync();

        var removed = await new RemoveReportHandler(db, clock).Handle(new RemoveReportRequest(staff.Id, reportId, "Contenido retirado."), CancellationToken.None);
        Assert.True(removed.IsSuccess);
        var action = await db.ModerationActions.SingleAsync();
        Assert.Equal(staff.Id, action.StaffId);
        Assert.Equal(ModerationActionType.RemoveContent, action.Action);
        Assert.Equal(reportId, action.TargetId);
        Assert.Equal("Contenido retirado.", action.Reason);
        Assert.Equal(ReportStatus.Removed, (await db.Reports.SingleAsync(item => item.Id == reportId)).Status);
    }

    private static Task<GetSessionProfileResponse> Profile(IdentityTestDb db, FixedClock clock, Guid userId) =>
        new GetSessionProfileHandler(db, clock, new Signer(), new FakeLinks()).Handle(new GetSessionProfileRequest(userId), CancellationToken.None)
            .ContinueWith(task => task.Result.Value!, TaskScheduler.Default);

    private static async Task<SubscriptionPlan> PlanAsync(
        IdentityTestDb db,
        string code,
        PlanTier tier,
        int reportsPerDay,
        int anonymousPerDay,
        bool showsAds,
        int devices)
    {
        var plan = SubscriptionPlan.Create(code, code, "Plan de prueba.", tier, 0, showsAds, devices, 15, reportsPerDay, anonymousPerDay, 0, false);
        plan.MarkCreated(Now);
        db.SubscriptionPlans.Add(plan);
        await db.SaveChangesAsync();
        return plan;
    }

    private static async Task<User> UserAsync(IdentityTestDb db, FixedClock clock, int years, string email, string userName, bool confirm, bool guardian)
    {
        Guid country;
        Guid region;
        Guid city;
        if (!await db.Countries.AnyAsync())
        {
            var countryEntity = Country.Create("CO", "Colombia", "Departamento", "es-CO", "COP", "America/Bogota", DistanceUnit.Kilometers, 14, true);
            var regionEntity = Region.Create(countryEntity.Id, "Cundinamarca", "25");
            var cityEntity = City.Create(regionEntity.Id, countryEntity.Id, "Bogotá", new GeoPoint(4.711, -74.072), "America/Bogota");
            country = countryEntity.Id;
            region = regionEntity.Id;
            city = cityEntity.Id;
            db.AddRange(countryEntity, regionEntity, cityEntity);
            await db.SaveChangesAsync();
        }
        else
        {
            country = await db.Countries.Select(item => item.Id).FirstAsync();
            region = await db.Regions.Select(item => item.Id).FirstAsync();
            city = await db.Cities.Select(item => item.Id).FirstAsync();
        }

        var user = User.Register(
            Email.Create(email),
            UserName.Create(userName),
            PersonName.Create("Ana"),
            PersonName.Create("Pérez"),
            PhoneNumber.Create("3001234567"),
            "hash",
            DateOnly.FromDateTime(clock.UtcNow.UtcDateTime).AddYears(-years),
            country,
            region,
            city,
            "es-CO",
            14,
            clock.UtcNow);
        if (confirm)
        {
            user.ConfirmEmail(clock.UtcNow);
        }

        if (guardian)
        {
            user.GrantGuardianConsent(clock.UtcNow);
        }

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private sealed class Signer : IMediaUrlSigner
    {
        public (long ExpiresUnix, string Signature) Sign(Guid mediaId, DateTimeOffset expiresAt) => (expiresAt.ToUnixTimeSeconds(), "sig");

        public bool IsValid(Guid mediaId, long expiresUnix, string signature, DateTimeOffset now) => signature == "sig";
    }
}
