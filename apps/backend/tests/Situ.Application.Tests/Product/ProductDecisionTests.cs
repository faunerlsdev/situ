using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Features.Administration.Settings;
using Situ.Application.Features.Administration.Takedowns;
using Situ.Application.Features.Administration.Users;
using Situ.Application.Features.Identity.ChangePhone;
using Situ.Application.Features.Identity.GetSessionProfile;
using Situ.Application.Features.Identity.RegisterUser;
using Situ.Application.Features.Legal.AcknowledgeAnonymityNotice;
using Situ.Application.Features.Reports.ConfirmReport;
using Situ.Application.Features.Reports.GetPublicReportMap;
using Situ.Application.Features.Reports.GetReportShare;
using Situ.Application.Features.Retention.RequestPersonalDataExport;
using Situ.Application.Tests.Support;
using Situ.Domain.Administration;
using Situ.Domain.Common;
using Situ.Domain.Identity;
using Situ.Domain.Profiles;
using Situ.Domain.Reports;

namespace Situ.Application.Tests.Product;

public sealed class ProductDecisionTests
{
    private static readonly DateTimeOffset Now = RegistrationCatalog.Now;

    [Fact]
    public void New_profiles_start_anonymous_and_an_explicit_choice_stays()
    {
        var created = UserProfile.CreateDefault(Guid.CreateVersion7(), "Ana");
        Assert.True(created.PreferAnonymousReports);
        created.SetPreferAnonymousReports(false);
        Assert.False(created.PreferAnonymousReports);
    }

    [Fact]
    public async Task New_adult_and_minor_prefer_anonymous_reports()
    {
        await using var db = IdentityTestDb.Create();
        var catalog = await RegistrationCatalog.SeedAsync(db);
        var clock = new FixedClock(Now);
        var handler = new RegisterUserHandler(db, clock, new FakePasswords(), new FakeEmailSender(), new FakeLinks());
        var adult = await handler.Handle(RegistrationCatalog.Request(catalog, 30), CancellationToken.None);
        var minorRequest = RegistrationCatalog.Request(catalog, 16, email: "menor@situ.test") with { UserName = "menor" };
        var minor = await handler.Handle(minorRequest, CancellationToken.None);
        Assert.True(adult.IsSuccess);
        Assert.True(minor.IsSuccess);

        var session = new GetSessionProfileHandler(db, clock, new Signer(), new FakeLinks());
        var adultProfile = await session.Handle(new GetSessionProfileRequest(adult.Value!.UserId), CancellationToken.None);
        var minorProfile = await session.Handle(new GetSessionProfileRequest(minor.Value!.UserId), CancellationToken.None);
        Assert.True(adultProfile.Value!.Profile.PreferAnonymousReports);
        Assert.True(minorProfile.Value!.Profile.PreferAnonymousReports);
        Assert.False(adultProfile.Value.AnonymityNoticeAccepted);
    }

    [Fact]
    public async Task Phone_is_personal_data_without_uniqueness_or_logs()
    {
        await using var db = IdentityTestDb.Create();
        var catalog = await RegistrationCatalog.SeedAsync(db);
        var clock = new FixedClock(Now);
        var register = new RegisterUserHandler(db, clock, new FakePasswords(), new FakeEmailSender(), new FakeLinks());
        var first = RegistrationCatalog.Request(catalog, 30);
        var second = first with { Email = "otra@situ.test", UserName = "otra" };
        Assert.True((await register.Handle(first, CancellationToken.None)).IsSuccess);
        Assert.True((await register.Handle(second, CancellationToken.None)).IsSuccess);

        db.SystemSettings.Add(SystemSetting.Create(SystemSettingKeys.PhoneRequiredForMinors, "false", "Menores.", Now));
        await db.SaveChangesAsync();
        var minor = RegistrationCatalog.Request(catalog, 16, email: "menor@situ.test") with { UserName = "menor", Phone = "" };
        var optional = await register.Handle(minor, CancellationToken.None);
        Assert.True(optional.IsSuccess);
        Assert.Null((await db.Users.SingleAsync(item => item.Id == optional.Value!.UserId)).Phone);

        var adultEmpty = RegistrationCatalog.Request(catalog, 30, email: "sin@situ.test") with { UserName = "sin", Phone = "" };
        Assert.Equal("phone.invalid", (await register.Handle(adultEmpty, CancellationToken.None)).Error!.Code);

        var leaked = "999000111";
        var invalid = await register.Handle(first with { Email = "mala@situ.test", UserName = "mala", Phone = leaked }, CancellationToken.None);
        Assert.DoesNotContain(leaked, invalid.Error!.Message);

        var owner = await db.Users.SingleAsync(item => item.UserName == UserName.Create("ana"));
        var files = new MemoryFiles();
        var export = await new RequestPersonalDataExportHandler(db, clock, files).Handle(new RequestPersonalDataExportRequest(owner.Id), CancellationToken.None);
        Assert.True(export.IsSuccess);
        Assert.Contains("+573001234567", Encoding.UTF8.GetString(files.Bytes[files.Keys[0]]), StringComparison.Ordinal);

        var changed = await new ChangePhoneHandler(db, clock, new FakePasswords()).Handle(
            new ChangePhoneRequest(owner.Id, "127.0.0.1", "Clave123", "+573009998877"),
            CancellationToken.None);
        Assert.True(changed.IsSuccess);
        var audit = await db.AuditLogs.SingleAsync(item => item.Action == "users.change_phone");
        Assert.DoesNotContain("3009998877", audit.Changes);
        Assert.DoesNotContain("+573009998877", audit.Changes);

        owner.RequestDeletion(Now);
        owner.CompleteDeletion(Now);
        Assert.Null(owner.Phone);
    }

    [Fact]
    public async Task Admin_phone_is_masked_and_absent_from_other_responses()
    {
        await using var db = IdentityTestDb.Create();
        var catalog = await RegistrationCatalog.SeedAsync(db);
        var clock = new FixedClock(Now);
        var register = new RegisterUserHandler(db, clock, new FakePasswords(), new FakeEmailSender(), new FakeLinks());
        await register.Handle(RegistrationCatalog.Request(catalog, 30), CancellationToken.None);
        var person = await db.Users.SingleAsync();
        var staff = User.Register(
            Email.Create("mod@situ.test"),
            UserName.Create("modera"),
            PersonName.Create("Mod"),
            PersonName.Create("Era"),
            PhoneNumber.Create("+573001110000"),
            "hash",
            new DateOnly(1990, 1, 1),
            catalog.CountryId,
            catalog.RegionId,
            catalog.CityId,
            "es-CO",
            14,
            Now);
        staff.ConfirmEmail(Now);
        db.Users.Add(staff);
        var role = StaffRole.Create("soporte", "Soporte", false, Now);
        db.StaffRoles.Add(role);
        db.StaffPermissions.Add(StaffPermission.Grant(role.Id, StaffPermissionCodes.UsersView));
        db.StaffPermissions.Add(StaffPermission.Grant(role.Id, StaffPermissionCodes.UsersViewEmail));
        db.StaffAssignments.Add(StaffAssignment.Assign(staff.Id, role.Id, null, Now));
        await db.SaveChangesAsync();

        var handler = new GetAdminUserHandler(db, clock);
        var hidden = await handler.Handle(new GetAdminUserRequest(staff.Id, "10.0.0.1", person.Id, false), CancellationToken.None);
        Assert.Null(hidden.Value!.MaskedPhone);
        var shown = await handler.Handle(new GetAdminUserRequest(staff.Id, "10.0.0.1", person.Id, false, true), CancellationToken.None);
        Assert.Equal("+57 ••• ••• 4567", shown.Value!.MaskedPhone);
        var phoneAudit = await db.AuditLogs.SingleAsync(item => item.Action == "users.view_phone");
        Assert.DoesNotContain("3001234567", phoneAudit.Changes);

        var search = await new SearchAdminUsersHandler(db, clock).Handle(new SearchAdminUsersRequest(staff.Id, "ana"), CancellationToken.None);
        var json = JsonSerializer.Serialize(search.Value);
        Assert.DoesNotContain("3001234567", json, StringComparison.Ordinal);
        Assert.DoesNotContain("phone", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Boolean_setting_rejects_anything_but_true_or_false()
    {
        await using var db = IdentityTestDb.Create();
        var catalog = await RegistrationCatalog.SeedAsync(db);
        var clock = new FixedClock(Now);
        var staff = User.Register(
            Email.Create("cfg@situ.test"),
            UserName.Create("config"),
            PersonName.Create("Con"),
            PersonName.Create("Fig"),
            PhoneNumber.Create("+573001110001"),
            "hash",
            new DateOnly(1990, 1, 1),
            catalog.CountryId,
            catalog.RegionId,
            catalog.CityId,
            "es-CO",
            14,
            Now);
        staff.ConfirmEmail(Now);
        db.Users.Add(staff);
        var role = StaffRole.Create("admin", "Admin", false, Now);
        db.StaffRoles.Add(role);
        db.StaffPermissions.Add(StaffPermission.Grant(role.Id, StaffPermissionCodes.SettingsManage));
        db.StaffAssignments.Add(StaffAssignment.Assign(staff.Id, role.Id, null, Now));
        await db.SaveChangesAsync();

        var handler = new UpdateSettingHandler(db, clock);
        var rejected = await handler.Handle(new UpdateSettingRequest(staff.Id, "10.0.0.1", SystemSettingKeys.PhoneRequiredForMinors, "si"), CancellationToken.None);
        Assert.Equal("settings.invalid_value", rejected.Error!.Code);
        var accepted = await handler.Handle(new UpdateSettingRequest(staff.Id, "10.0.0.1", SystemSettingKeys.PhoneRequiredForMinors, "false"), CancellationToken.None);
        Assert.True(accepted.IsSuccess);
    }

    [Fact]
    public async Task Public_map_hides_minors_authors_and_extra_pins()
    {
        await using var db = IdentityTestDb.Create();
        var catalog = await RegistrationCatalog.SeedAsync(db);
        var adult = Person(db, catalog, "adulta", 30, "+573001234567");
        var minor = Person(db, catalog, "menor", 16, "+573009990000");
        var category = ReportCategory.Create("evento", "Evento", "Descripción de la categoría de prueba.", "/a.png", "/b.png", "#112233", 1, 90, 5);
        db.ReportCategories.Add(category);
        db.Reports.Add(Report.Create(adult.Id, category.Id, catalog.CityId, new GeoPoint(4.71104, -74.07219), "Texto secreto del chisme", true, false, TimeSpan.FromHours(2), Now));
        db.Reports.Add(Report.Create(minor.Id, category.Id, catalog.CityId, new GeoPoint(4.71104, -74.07219), "Menor", true, true, TimeSpan.FromHours(2), Now));
        db.Reports.Add(Report.Create(adult.Id, category.Id, catalog.CityId, new GeoPoint(4.71104, -74.07219), "Amigos", true, false, TimeSpan.FromHours(2), Now, visibility: ContentVisibility.FriendsOnly));
        await db.SaveChangesAsync();

        var handler = new GetPublicReportMapHandler(db, new FixedClock(Now));
        var result = await handler.Handle(new GetPublicReportMapRequest(4, -75, 5, -74), CancellationToken.None);
        var pin = Assert.Single(result.Value!.Items);
        Assert.Equal("Evento", pin.CategoryName);
        Assert.Equal(4.711, pin.Latitude);
        Assert.Equal(-74.072, pin.Longitude);
        Assert.Equal("Bogotá", pin.Zone);
        Assert.EndsWith("T15:00:00Z", pin.ApproximateTime, StringComparison.Ordinal);
        var json = JsonSerializer.Serialize(result.Value);
        Assert.DoesNotContain("Texto secreto", json, StringComparison.Ordinal);
        Assert.DoesNotContain("3001234567", json, StringComparison.Ordinal);
        Assert.DoesNotContain("author", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UserId", typeof(GetPublicReportMapRequest).GetProperties().Select(item => item.Name));
    }

    [Fact]
    public async Task Public_map_stops_at_fifty_pins()
    {
        await using var db = IdentityTestDb.Create();
        var catalog = await RegistrationCatalog.SeedAsync(db);
        var adult = Person(db, catalog, "adulta", 30, "+573001234567");
        var category = ReportCategory.Create("evento", "Evento", "Descripción de la categoría de prueba.", "/a.png", "/b.png", "#112233", 1, 90, 5);
        db.ReportCategories.Add(category);
        for (var index = 0; index < 60; index++)
        {
            db.Reports.Add(Report.Create(adult.Id, category.Id, catalog.CityId, new GeoPoint(4.71, -74.07), "Reporte", true, false, TimeSpan.FromHours(2), Now.AddMinutes(-index)));
        }

        await db.SaveChangesAsync();
        var result = await new GetPublicReportMapHandler(db, new FixedClock(Now)).Handle(new GetPublicReportMapRequest(4, -75, 5, -74), CancellationToken.None);
        Assert.Equal(PublicReadLimits.MaxPins, result.Value!.Items.Count);
    }

    [Fact]
    public async Task Share_card_is_identical_when_missing_private_or_minor()
    {
        await using var db = IdentityTestDb.Create();
        var catalog = await RegistrationCatalog.SeedAsync(db);
        var adult = Person(db, catalog, "adulta", 30, "+573001234567");
        var minor = Person(db, catalog, "menor", 16, "+573009990000");
        var category = ReportCategory.Create("evento", "Evento", "Descripción de la categoría de prueba.", "/a.png", "/b.png", "#112233", 1, 90, 5);
        db.ReportCategories.Add(category);
        var friends = Report.Create(adult.Id, category.Id, catalog.CityId, new GeoPoint(4.7, -74.0), "Privado", false, false, TimeSpan.FromHours(2), Now, visibility: ContentVisibility.FriendsOnly);
        var child = Report.Create(minor.Id, category.Id, catalog.CityId, new GeoPoint(4.7, -74.0), "Menor", true, true, TimeSpan.FromHours(2), Now);
        db.Reports.AddRange(friends, child);
        await db.SaveChangesAsync();

        var handler = new GetReportShareHandler(db, new FixedClock(Now));
        var missing = await handler.Handle(new GetReportShareRequest(Guid.CreateVersion7()), CancellationToken.None);
        var hidden = await handler.Handle(new GetReportShareRequest(friends.Id), CancellationToken.None);
        var young = await handler.Handle(new GetReportShareRequest(child.Id), CancellationToken.None);
        Assert.Equal(JsonSerializer.Serialize(missing.Value), JsonSerializer.Serialize(hidden.Value));
        Assert.Equal(JsonSerializer.Serialize(missing.Value), JsonSerializer.Serialize(young.Value));
        Assert.False(missing.Value!.Indexable);
    }

    [Fact]
    public async Task Confirmation_does_not_store_the_voter_location()
    {
        await using var db = IdentityTestDb.Create();
        var catalog = await RegistrationCatalog.SeedAsync(db);
        var author = Person(db, catalog, "autor", 30, "+573001234567");
        var voter = Person(db, catalog, "voto", 30, "+573001234568");
        var category = ReportCategory.Create("evento", "Evento", "Descripción de la categoría de prueba.", "/a.png", "/b.png", "#112233", 1, 90, 5);
        db.ReportCategories.Add(category);
        var report = Report.Create(author.Id, category.Id, catalog.CityId, new GeoPoint(4.711, -74.072), "Sigue", true, false, TimeSpan.FromHours(2), Now);
        db.Reports.Add(report);
        await db.SaveChangesAsync();

        var result = await new ConfirmReportHandler(db, new FixedClock(Now)).Handle(
            new ConfirmReportRequest(voter.Id, report.Id, ConfirmationType.StillThere, 4.711, -74.072),
            CancellationToken.None);
        Assert.True(result.IsSuccess);
        var stored = await db.ReportConfirmations.SingleAsync();
        Assert.Null(stored.VoterLocation);
    }

    [Fact]
    public async Task Captcha_failure_stores_nothing_and_success_stays_neutral()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var mail = new FakeEmailSender();
        var broken = new SubmitTakedownHandler(db, clock, mail, new ThrowingCaptcha());
        var failed = await broken.Handle(Takedown("ana@correo.com", null), CancellationToken.None);
        Assert.Equal("captcha.invalid", failed.Error!.Code);
        Assert.Empty(db.TakedownRequests);

        var handler = new SubmitTakedownHandler(db, clock, mail, new AcceptCaptcha(true));
        var missing = await handler.Handle(Takedown("ana@correo.com", null), CancellationToken.None);
        var expired = await handler.Handle(Takedown("ana@correo.com", "expired"), CancellationToken.None);
        Assert.Equal("captcha.invalid", missing.Error!.Code);
        Assert.Equal("captcha.invalid", expired.Error!.Code);
        Assert.Empty(db.TakedownRequests);

        var accepted = await handler.Handle(Takedown("ana@correo.com", "dev-ok"), CancellationToken.None);
        Assert.True(accepted.Value!.Accepted);
        Assert.StartsWith("TD-", accepted.Value.TrackingCode);
        Assert.DoesNotContain("existe", accepted.Value.TrackingCode, StringComparison.OrdinalIgnoreCase);
        Assert.Single(db.TakedownRequests);
        Assert.Contains("seguimiento", mail.Messages[0].Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Public_read_limits_stop_the_next_request()
    {
        Assert.Equal(30, PublicReadLimits.MapPermitsPerMinute);
        Assert.Equal(60, PublicReadLimits.SharePermitsPerMinute);
        Assert.Equal(50, PublicReadLimits.MaxPins);
    }

    [Fact]
    public async Task Anonymity_notice_is_stored_once()
    {
        await using var db = IdentityTestDb.Create();
        var catalog = await RegistrationCatalog.SeedAsync(db);
        var clock = new FixedClock(Now);
        var register = new RegisterUserHandler(db, clock, new FakePasswords(), new FakeEmailSender(), new FakeLinks());
        var created = await register.Handle(RegistrationCatalog.Request(catalog, 30), CancellationToken.None);
        var handler = new AcknowledgeAnonymityNoticeHandler(db, clock);
        var first = await handler.Handle(new AcknowledgeAnonymityNoticeRequest(created.Value!.UserId, "127.0.0.1", "tests"), CancellationToken.None);
        var second = await handler.Handle(new AcknowledgeAnonymityNoticeRequest(created.Value.UserId, "127.0.0.1", "tests"), CancellationToken.None);
        Assert.Equal(AnonymityNotice.Version, first.Value!.Version);
        Assert.Equal(first.Value.AcceptedAt, second.Value!.AcceptedAt);
        Assert.Equal(1, await db.ConsentRecords.CountAsync(item => item.DocumentType == Situ.Domain.Legal.LegalDocumentType.AnonymityNotice));
    }

    private static SubmitTakedownRequest Takedown(string email, string? captcha) =>
        new("Ana Pérez", email, "Report", Guid.CreateVersion7(), "Aparezco en este reporte y pido el retiro.", null, 3000, "10.1.1.1", captcha);

    private static User Person(IdentityTestDb db, Catalog catalog, string name, int years, string phone)
    {
        var user = User.Register(
            Email.Create($"{name}@situ.test"),
            UserName.Create(name),
            PersonName.Create("Ana"),
            PersonName.Create("Pérez"),
            PhoneNumber.Create(phone),
            "hash",
            DateOnly.FromDateTime(Now.UtcDateTime).AddYears(-years),
            catalog.CountryId,
            catalog.RegionId,
            catalog.CityId,
            "es-CO",
            14,
            Now);
        user.ConfirmEmail(Now);
        if (user.IsMinor(Now))
        {
            user.GrantGuardianConsent(Now);
        }

        db.Users.Add(user);
        return user;
    }

    private sealed class Signer : IMediaUrlSigner
    {
        public (long ExpiresUnix, string Signature) Sign(Guid mediaId, DateTimeOffset expiresAt) => (0, "sig");

        public bool IsValid(Guid mediaId, long expiresUnix, string signature, DateTimeOffset now) => false;
    }

    private sealed class ThrowingCaptcha : ICaptchaVerifier
    {
        public Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken) =>
            throw new HttpRequestException("captcha down");
    }

    private sealed class MemoryFiles : IFileStorage
    {
        public List<string> Keys { get; } = [];
        public Dictionary<string, byte[]> Bytes { get; } = [];

        public Task SaveAsync(string key, byte[] content, CancellationToken cancellationToken)
        {
            Keys.Add(key);
            Bytes[key] = content;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken) =>
            Task.FromResult<Stream?>(Bytes.TryGetValue(key, out var content) ? new MemoryStream(content) : null);
    }
}
