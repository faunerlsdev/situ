using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Reports;
using Situ.Application.Features.Reports.ConfirmReport;
using Situ.Application.Features.Reports.CreateReport;
using Situ.Application.Features.Reports.DeleteReport;
using Situ.Application.Features.Reports.GetReport;
using Situ.Application.Features.Reports.GetReportCatalog;
using Situ.Application.Features.Reports.GetReportMap;
using Situ.Application.Features.Reports.ListMyReports;
using Situ.Application.Features.Reports.UpdateReport;
using Situ.Domain.Common;
using Situ.Domain.Geography;
using Situ.Domain.Identity;
using Situ.Domain.Monetization;
using Situ.Domain.Reports;
using Situ.Domain.Social;
using Situ.Application.Tests.Support;
using Situ.Infrastructure.Reports;

namespace Situ.Application.Tests.Reports;

public class ReportFlowTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Creates_a_report_and_applies_the_city_ttl_override()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var (user, category, city) = await SeedAsync(db, clock, 30, reportsPerDay: 10);
        db.ReportCategoryOverrides.Add(ReportCategoryOverride.Create(category.Id, null, city, true, 30));
        await db.SaveChangesAsync();

        var result = await Create(db, clock).Handle(Request(user, category.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Now.AddMinutes(30), result.Value!.Report.ExpiresAt);
        Assert.Equal(user.Id, result.Value.Report.Author.UserId);
    }

    [Fact]
    public async Task Rejects_a_report_that_uses_a_blocked_term()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var (user, category, _) = await SeedAsync(db, clock, 30, reportsPerDay: 10);

        var result = await Create(db, clock).Handle(Request(user, category.Id, "Esto es porn y no se publica."), CancellationToken.None);

        Assert.Equal("content.not_allowed", result.Error!.Code);
    }

    [Fact]
    public async Task Hides_adult_categories_from_minors_and_blocks_unconfirmed_accounts()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var adult = await UserAsync(db, clock, 30, "adult@situ.test", "adult", confirm: true, guardian: false);
        var minor = await UserAsync(db, clock, 16, "minor@situ.test", "minor", confirm: true, guardian: true);
        var pending = await UserAsync(db, clock, 30, "pending@situ.test", "pending", confirm: false, guardian: false);
        var waiting = await UserAsync(db, clock, 15, "wait@situ.test", "waiting", confirm: true, guardian: false);
        await PlanAsync(db, 10, 3);
        var adultCategory = Category("solo_adultos", MediaRequirement.None, adultOnly: true);
        var open = Category("puesto_control", MediaRequirement.None, adultOnly: false);
        db.ReportCategories.AddRange(adultCategory, open);
        await db.SaveChangesAsync();

        var catalog = await new GetReportCatalogHandler(db, clock).Handle(new GetReportCatalogRequest(minor.Id, "es-CO"), CancellationToken.None);
        Assert.DoesNotContain(catalog.Value!.Categories, item => item.Code == "solo_adultos");

        var adultOnly = await Create(db, clock).Handle(Request(minor, adultCategory.Id, anonymous: true), CancellationToken.None);
        Assert.Equal("report.adult_only", adultOnly.Error!.Code);

        var unconfirmed = await Create(db, clock).Handle(Request(pending, open.Id), CancellationToken.None);
        Assert.Equal("report.email_unconfirmed", unconfirmed.Error!.Code);

        var guardian = await Create(db, clock).Handle(Request(waiting, open.Id), CancellationToken.None);
        Assert.Equal("report.pending_guardian", guardian.Error!.Code);
        _ = adult;
    }

    [Fact]
    public async Task Enforces_the_free_plan_and_the_minor_visibility_rule()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var user = await UserAsync(db, clock, 30, "ana@situ.test", "ana", true, false);
        var minor = await UserAsync(db, clock, 16, "lia@situ.test", "lia", true, true);
        await PlanAsync(db, 1, 1);
        var category = Category("evento", MediaRequirement.None, false);
        db.ReportCategories.Add(category);
        await db.SaveChangesAsync();

        Assert.True((await Create(db, clock).Handle(Request(user, category.Id), CancellationToken.None)).IsSuccess);
        var limited = await Create(db, clock).Handle(Request(user, category.Id, "Otro reporte en el mismo día."), CancellationToken.None);
        Assert.Equal("report.daily_limit", limited.Error!.Code);
        Assert.Equal(ErrorType.RateLimited, limited.Error.Type);

        var named = await Create(db, clock).Handle(Request(minor, category.Id, visibility: ContentVisibility.Public, anonymous: false), CancellationToken.None);
        Assert.Equal("report.minor_public_identity", named.Error!.Code);
        var hidden = await Create(db, clock).Handle(Request(minor, category.Id, "Voy anónimo.", visibility: ContentVisibility.Public, anonymous: true), CancellationToken.None);
        Assert.True(hidden.IsSuccess);
        Assert.Null(hidden.Value!.Report.Author.UserId);
        Assert.True(hidden.Value.Report.HideAuthorOnMap);
    }

    [Fact]
    public async Task Requires_media_when_the_category_asks_for_it_and_only_the_author_edits()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var author = await UserAsync(db, clock, 30, "ana@situ.test", "ana", true, false);
        var other = await UserAsync(db, clock, 30, "other@situ.test", "other", true, false);
        await PlanAsync(db, 10, 3);
        var category = Category("hurto", MediaRequirement.PhotoOrVideoRequired, false);
        db.ReportCategories.Add(category);
        await db.SaveChangesAsync();

        var missing = await Create(db, clock).Handle(Request(author, category.Id), CancellationToken.None);
        Assert.Equal("report.media_required", missing.Error!.Code);

        var media = ReportMedia.Stage(author.Id, MediaType.Image, "reports/one", 10, null, false, Now);
        media.MarkReady(null);
        db.ReportMedias.Add(media);
        await db.SaveChangesAsync();
        var created = await Create(db, clock).Handle(Request(author, category.Id, mediaIds: [media.Id]), CancellationToken.None);
        Assert.True(created.IsSuccess);

        var stranger = await new UpdateReportHandler(db, clock).Handle(new UpdateReportRequest(other.Id, created.Value!.Report.Id, "Cambio ajeno del texto."), CancellationToken.None);
        Assert.Equal("report.not_author", stranger.Error!.Code);
        var edited = await new UpdateReportHandler(db, clock).Handle(new UpdateReportRequest(author.Id, created.Value.Report.Id, "Sigue el hurto en la esquina."), CancellationToken.None);
        Assert.True(edited.IsSuccess);
        var deleted = await new DeleteReportHandler(db, clock).Handle(new DeleteReportRequest(other.Id, created.Value.Report.Id), CancellationToken.None);
        Assert.Equal("report.not_author", deleted.Error!.Code);
        Assert.True((await new DeleteReportHandler(db, clock).Handle(new DeleteReportRequest(author.Id, created.Value.Report.Id), CancellationToken.None)).IsSuccess);
    }

    [Fact]
    public async Task Anonymous_report_responses_never_expose_the_author()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var author = await UserAsync(db, clock, 30, "ana@situ.test", "ana", true, false);
        var viewer = await UserAsync(db, clock, 30, "viewer@situ.test", "viewer", true, false);
        await PlanAsync(db, 10, 3);
        var category = Category("evento", MediaRequirement.None, false);
        db.ReportCategories.Add(category);
        await db.SaveChangesAsync();
        var created = await Create(db, clock).Handle(Request(author, category.Id, anonymous: true), CancellationToken.None);
        var reportId = created.Value!.Report.Id;

        var detail = await new GetReportHandler(db, clock, new FakeSigner()).Handle(new GetReportRequest(viewer.Id, reportId), CancellationToken.None);
        var mine = await new ListMyReportsHandler(db, new FakeSigner(), clock).Handle(new ListMyReportsRequest(author.Id, null), CancellationToken.None);
        var map = await new GetReportMapHandler(db, clock, new ReportMapReader(db, clock), new FakeSigner()).Handle(
            new GetReportMapRequest(viewer.Id, 4, -75, 5, -74, 16, [], null, null, 4.7, -74.07),
            CancellationToken.None);

        var payloads = new object?[] { detail.Value, mine.Value, map.Value, created.Value };
        foreach (var payload in payloads)
        {
            var json = JsonSerializer.Serialize(payload);
            Assert.DoesNotContain(author.Id.ToString(), json, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(Now.Year, detail.Value!.Report.Author.MemberSinceYear);
        Assert.Equal(Now.Month, detail.Value.Report.Author.MemberSinceMonth);
        Assert.Null(detail.Value.Report.Author.AvatarUrl);
        Assert.True(detail.Value.Report.HideAuthorOnMap);
        Assert.DoesNotContain(
            typeof(ReportAuthorView).GetProperties(),
            property => property.Name.Contains("Day", StringComparison.OrdinalIgnoreCase));
        foreach (var type in typeof(ReportAuthorView).Assembly.GetTypes().Where(type =>
            type.Namespace?.Contains(".Features.Reports", StringComparison.Ordinal) == true
            && (type.Name.EndsWith("Response", StringComparison.Ordinal) || type.Name.EndsWith("View", StringComparison.Ordinal))))
        {
            Assert.Null(type.GetProperty("AuthorId", BindingFlags.Instance | BindingFlags.Public));
        }
    }

    [Fact]
    public async Task Confirmation_replaces_the_vote_extends_life_and_can_expire_the_report()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var author = await UserAsync(db, clock, 30, "ana@situ.test", "ana", true, false);
        var voter = await UserAsync(db, clock, 30, "voter@situ.test", "voter", true, false);
        await PlanAsync(db, 10, 3);
        var category = Category("incendio", MediaRequirement.None, false, ttl: 100);
        db.ReportCategories.Add(category);
        await db.SaveChangesAsync();
        var reportId = (await Create(db, clock).Handle(Request(author, category.Id, latitude: 4.711, longitude: -74.072), CancellationToken.None)).Value!.Report.Id;

        var own = await Confirm(db, clock).Handle(new ConfirmReportRequest(author.Id, reportId, ConfirmationType.StillThere, 4.711, -74.072), CancellationToken.None);
        Assert.Equal("report.confirmation_own", own.Error!.Code);
        var far = await Confirm(db, clock).Handle(new ConfirmReportRequest(voter.Id, reportId, ConfirmationType.StillThere, 6.24, -75.58), CancellationToken.None);
        Assert.Equal("report.confirmation_too_far", far.Error!.Code);

        var first = await Confirm(db, clock).Handle(new ConfirmReportRequest(voter.Id, reportId, ConfirmationType.StillThere, 4.711, -74.072), CancellationToken.None);
        Assert.Equal(Now.AddMinutes(100).AddMinutes(25), first.Value!.ExpiresAt);
        var changed = await Confirm(db, clock).Handle(new ConfirmReportRequest(voter.Id, reportId, ConfirmationType.GoneNow, 4.712, -74.072), CancellationToken.None);
        Assert.Equal(0, changed.Value!.StillThereCount);
        Assert.Equal(1, changed.Value.GoneNowCount);
        Assert.Equal(1, await db.ReportConfirmations.CountAsync());

        for (var index = 0; index < 2; index++)
        {
            var extra = await UserAsync(db, clock, 30, $"v{index}@situ.test", $"voter{index}", true, false);
            await Confirm(db, clock).Handle(new ConfirmReportRequest(extra.Id, reportId, ConfirmationType.GoneNow, 4.711, -74.072), CancellationToken.None);
        }

        var stored = await db.Reports.FirstAsync(item => item.Id == reportId);
        Assert.Equal(ReportStatus.Expired, stored.Status);
    }

    [Fact]
    public async Task Map_respects_the_viewport_friends_blocks_and_expiry()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var author = await UserAsync(db, clock, 30, "ana@situ.test", "ana", true, false);
        var friend = await UserAsync(db, clock, 30, "friend@situ.test", "friend", true, false);
        var stranger = await UserAsync(db, clock, 30, "stranger@situ.test", "stranger", true, false);
        await PlanAsync(db, 10, 3);
        var category = Category("evento", MediaRequirement.None, false);
        db.ReportCategories.Add(category);
        await db.SaveChangesAsync();
        var near = (await Create(db, clock).Handle(Request(author, category.Id, latitude: 4.711, longitude: -74.072), CancellationToken.None)).Value!.Report.Id;
        await Create(db, clock).Handle(Request(author, category.Id, "Lejos.", latitude: 6.244, longitude: -75.581), CancellationToken.None);
        var friendsOnly = (await Create(db, clock).Handle(Request(author, category.Id, "Solo amigos.", latitude: 4.72, longitude: -74.08, visibility: ContentVisibility.FriendsOnly), CancellationToken.None)).Value!.Report.Id;
        var finished = (await Create(db, clock).Handle(Request(author, category.Id, "Ya pasó.", latitude: 4.73, longitude: -74.09), CancellationToken.None)).Value!.Report.Id;
        var expired = await db.Reports.FirstAsync(item => item.Id == finished);
        expired.Expire(Now);
        await db.SaveChangesAsync();

        var reader = new ReportMapReader(db, clock);
        var handler = new GetReportMapHandler(db, clock, reader, new FakeSigner());
        var bogota = await handler.Handle(new GetReportMapRequest(stranger.Id, 4.5, -74.3, 4.9, -73.9, 16, [], null, null, null, null), CancellationToken.None);
        Assert.Contains(bogota.Value!.Items, item => item.Id == near && item.Kind == "point");
        Assert.DoesNotContain(bogota.Value.Items, item => item.Id == friendsOnly);
        Assert.DoesNotContain(bogota.Value.Items, item => item.Id == finished);

        var clustered = await handler.Handle(new GetReportMapRequest(stranger.Id, 0, -80, 10, -70, 3, [], null, null, null, null), CancellationToken.None);
        Assert.Contains(clustered.Value!.Items, item => item.Kind == "cluster" && item.Count >= 2);

        var friendship = Friendship.Request(author.Id, friend.Id, Now);
        friendship.Accept(friend.Id, Now);
        db.Friendships.Add(friendship);
        await db.SaveChangesAsync();
        var asFriend = await handler.Handle(new GetReportMapRequest(friend.Id, 4.5, -74.3, 4.9, -73.9, 16, [category.Code], null, "friends", null, null), CancellationToken.None);
        Assert.Contains(asFriend.Value!.Items, item => item.Id == friendsOnly);

        db.UserBlocks.Add(UserBlock.Create(stranger.Id, author.Id, Now));
        await db.SaveChangesAsync();
        var blocked = await handler.Handle(new GetReportMapRequest(stranger.Id, 4.5, -74.3, 4.9, -73.9, 16, [], null, null, null, null), CancellationToken.None);
        Assert.Empty(blocked.Value!.Items);
    }

    [Fact]
    public async Task Map_marks_only_the_viewers_reports_including_anonymous_ones()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var author = await UserAsync(db, clock, 30, "ana@situ.test", "ana", true, false);
        var stranger = await UserAsync(db, clock, 30, "stranger@situ.test", "stranger", true, false);
        await PlanAsync(db, 10, 3);
        var category = Category("evento", MediaRequirement.None, false);
        db.ReportCategories.Add(category);
        await db.SaveChangesAsync();
        var hidden = (await Create(db, clock).Handle(Request(author, category.Id, "Voy anónimo.", anonymous: true), CancellationToken.None)).Value!.Report.Id;
        var named = (await Create(db, clock).Handle(Request(stranger, category.Id, "Lo vi yo."), CancellationToken.None)).Value!.Report.Id;

        var reader = new ReportMapReader(db, clock);
        var handler = new GetReportMapHandler(db, clock, reader, new FakeSigner());
        var asAuthor = await handler.Handle(new GetReportMapRequest(author.Id, 4.5, -74.3, 4.9, -73.9, 16, [], null, null, null, null), CancellationToken.None);
        var own = Assert.Single(asAuthor.Value!.Items, item => item.Id == hidden);
        Assert.True(own.IsMine);
        Assert.Null(own.Author!.UserId);
        Assert.True(own.IsAnonymous);
        Assert.False(asAuthor.Value.Items.Single(item => item.Id == named).IsMine);

        var asStranger = await handler.Handle(new GetReportMapRequest(stranger.Id, 4.5, -74.3, 4.9, -73.9, 16, [], null, null, null, null), CancellationToken.None);
        var seen = Assert.Single(asStranger.Value!.Items, item => item.Id == hidden);
        Assert.False(seen.IsMine);
        Assert.Null(seen.Author!.UserId);
        Assert.True(asStranger.Value.Items.Single(item => item.Id == named).IsMine);
    }

    private static CreateReportHandler Create(IdentityTestDb db, FixedClock clock) => new(db, clock, new FakeSigner());

    private static ConfirmReportHandler Confirm(IdentityTestDb db, FixedClock clock) => new(db, clock);

    private static CreateReportRequest Request(
        User user,
        Guid categoryId,
        string description = "Hay novedad en esta esquina ahora mismo.",
        double latitude = 4.711,
        double longitude = -74.072,
        ContentVisibility visibility = ContentVisibility.Public,
        bool anonymous = false,
        IReadOnlyList<Guid>? mediaIds = null) =>
        new(user.Id, categoryId, description, latitude, longitude, visibility, anonymous, mediaIds ?? []);

    private static async Task<(User User, ReportCategory Category, Guid CityId)> SeedAsync(
        IdentityTestDb db,
        FixedClock clock,
        int age,
        int reportsPerDay)
    {
        var user = await UserAsync(db, clock, age, "ana@situ.test", "ana", true, false);
        await PlanAsync(db, reportsPerDay, 3);
        var category = Category("accidente_transito", MediaRequirement.None, false, 90);
        db.ReportCategories.Add(category);
        await db.SaveChangesAsync();
        return (user, category, user.CityId);
    }

    private static ReportCategory Category(string code, MediaRequirement requirement, bool adultOnly, int ttl = 90)
    {
        var category = ReportCategory.Create(code, code, "Descripción de la categoría de prueba.", "/assets/categories/" + code + ".png", "/assets/categories/" + code + ".png", "#112233", 1, ttl, 5, requirement, isAdultOnly: adultOnly);
        category.MarkCreated(Now);
        return category;
    }

    private static async Task PlanAsync(IdentityTestDb db, int reportsPerDay, int anonymousPerDay)
    {
        var plan = SubscriptionPlan.Create("free", "Free", "Plan de prueba.", PlanTier.Free, 0, true, 2, 15, reportsPerDay, anonymousPerDay, 0, false);
        plan.MarkCreated(Now);
        db.SubscriptionPlans.Add(plan);
        await db.SaveChangesAsync();
    }

    private static async Task<User> UserAsync(IdentityTestDb db, FixedClock clock, int years, string email, string userName, bool confirm, bool guardian)
    {
        var country = Guid.NewGuid();
        var region = Guid.NewGuid();
        var city = Guid.NewGuid();
        if (!await db.Cities.AnyAsync(item => item.Id == city))
        {
            var countryEntity = Country.Create("CO", "Colombia", "Departamento", "es-CO", "COP", "America/Bogota", DistanceUnit.Kilometers, 14, true);
            var regionEntity = Region.Create(countryEntity.Id, "Cundinamarca", "25");
            var cityEntity = City.Create(regionEntity.Id, countryEntity.Id, "Bogotá", new GeoPoint(4.711, -74.072), "America/Bogota");
            country = countryEntity.Id;
            region = regionEntity.Id;
            city = cityEntity.Id;
            if (!await db.Countries.AnyAsync())
            {
                db.AddRange(countryEntity, regionEntity, cityEntity);
                await db.SaveChangesAsync();
            }
            else
            {
                country = await db.Countries.Select(item => item.Id).FirstAsync();
                region = await db.Regions.Select(item => item.Id).FirstAsync();
                city = await db.Cities.Select(item => item.Id).FirstAsync();
            }
        }

        var user = User.Register(
            Email.Create(email),
            UserName.Create(userName),
            PersonName.Create("Ana"),
            PersonName.Create("Pérez"),
            PhoneNumber.Create("+573001234567"),
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

    private sealed class FakeSigner : IMediaUrlSigner
    {
        public (long ExpiresUnix, string Signature) Sign(Guid mediaId, DateTimeOffset expiresAt) => (expiresAt.ToUnixTimeSeconds(), "sig");

        public bool IsValid(Guid mediaId, long expiresUnix, string signature, DateTimeOffset now) => signature == "sig";
    }
}
