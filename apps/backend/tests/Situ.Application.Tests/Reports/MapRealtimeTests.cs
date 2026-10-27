using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Situ.Application.Common.Abstractions;
using Situ.Application.Features.MapRealtime;
using Situ.Application.Features.MapRealtime.AuthorizeMapConnection;
using Situ.Application.Features.MapRealtime.PublishMapZoneNotices;
using Situ.Application.Features.Reports.GetReportBatch;
using Situ.Application.Tests.Support;
using Situ.Domain.Common;
using Situ.Domain.Geography;
using Situ.Domain.Identity;
using Situ.Domain.Reports;
using Situ.Domain.Social;
using Situ.Infrastructure.Persistence.Interceptors;

namespace Situ.Application.Tests.Reports;

public class MapRealtimeTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_public_adult_report_is_queued_only_when_dispatch_runs_and_a_private_one_is_not()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var author = await UserAsync(db, clock, 30, "ana@situ.test", "ana", "+573001111111");
        var category = Category("accidente");
        db.ReportCategories.Add(category);
        await db.SaveChangesAsync();
        var publicReport = Report.Create(author.Id, category.Id, author.CityId, new GeoPoint(4.711, -74.072), "Hay un choque.", true, false, TimeSpan.FromHours(2), Now);
        var friends = Report.Create(author.Id, category.Id, author.CityId, new GeoPoint(4.712, -74.073), "Solo amigos.", true, false, TimeSpan.FromHours(2), Now, visibility: ContentVisibility.FriendsOnly);
        var minor = Report.Create(author.Id, category.Id, author.CityId, new GeoPoint(4.713, -74.074), "De un menor.", true, true, TimeSpan.FromHours(2), Now);
        db.Reports.AddRange(publicReport, friends, minor);
        await db.SaveChangesAsync();
        Assert.Empty(await db.MapZoneNotices.ToListAsync());

        var dispatcher = new DomainEventDispatcher(new QuietLogger(), new RootScope(db, clock));
        await dispatcher.DispatchAsync(
        [
            new ReportCreatedEvent(publicReport.Id, category.Id, author.CityId, publicReport.Location, true),
            new ReportCreatedEvent(friends.Id, category.Id, author.CityId, friends.Location, true),
            new ReportCreatedEvent(minor.Id, category.Id, author.CityId, minor.Location, true),
            new ReportMapChanged(friends.Id, ReportMapChanged.Removed),
        ],
        CancellationToken.None);

        var notice = Assert.Single(await db.MapZoneNotices.ToListAsync());
        Assert.Equal(publicReport.Id, notice.ReportId);
        Assert.Equal(MapZoneNotice.Created, notice.Type);
        Assert.Equal("-1482:94", notice.CellId);
        Assert.Equal(MapZoneNoticeStatus.Pending, notice.Status);
    }

    [Fact]
    public async Task The_zone_signal_has_no_content_and_a_publisher_failure_keeps_the_notice_pending()
    {
        var signal = new MapZoneSignal(MapZoneNotice.Created, Guid.CreateVersion7(), "-1482:94", Now);
        var json = JsonSerializer.Serialize(signal, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var document = JsonDocument.Parse(json);
        Assert.Equal(
            ["cellId", "occurredAt", "reportId", "type"],
            document.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.DoesNotContain("description", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("author", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("userId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("latitude", json, StringComparison.OrdinalIgnoreCase);

        await using var db = IdentityTestDb.Create();
        db.MapZoneNotices.Add(MapZoneNotice.Enqueue(MapZoneNotice.Updated, signal.ReportId, signal.CellId, Now));
        await db.SaveChangesAsync();
        var handler = new PublishMapZoneNoticesHandler(db, new FixedClock(Now), new ThrowingPublisher());

        var result = await handler.Handle(new PublishMapZoneNoticesRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Failed);
        Assert.Equal(0, result.Value.Published);
        Assert.Equal(MapZoneNoticeStatus.Pending, Assert.Single(await db.MapZoneNotices.ToListAsync()).Status);
    }

    [Fact]
    public async Task A_published_batch_is_marked_only_after_the_publisher_accepts_it()
    {
        await using var db = IdentityTestDb.Create();
        var publisher = new RememberingPublisher();
        db.MapZoneNotices.Add(MapZoneNotice.Enqueue(MapZoneNotice.Created, Guid.CreateVersion7(), "-1482:94", Now));
        db.MapZoneNotices.Add(MapZoneNotice.Enqueue(MapZoneNotice.Removed, Guid.CreateVersion7(), "-1482:94", Now.AddSeconds(1)));
        await db.SaveChangesAsync();

        var result = await new PublishMapZoneNoticesHandler(db, new FixedClock(Now), publisher)
            .Handle(new PublishMapZoneNoticesRequest(), CancellationToken.None);

        Assert.Equal(2, result.Value!.Published);
        Assert.Equal(1, result.Value.Cells);
        Assert.Equal(2, Assert.Single(publisher.Batches).Count);
        Assert.All(await db.MapZoneNotices.ToListAsync(), notice => Assert.Equal(MapZoneNoticeStatus.Published, notice.Status));
    }

    [Fact]
    public async Task Dispatch_does_not_run_when_the_save_fails()
    {
        var counting = new CountingDispatcher();
        var options = new DbContextOptionsBuilder<IdentityTestDb>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new ThrowWhenReportIsAdded(), new DomainEventSaveChangesInterceptor(counting))
            .Options;
        await using var db = new IdentityTestDb(options);
        db.Database.EnsureCreated();
        var clock = new FixedClock(Now);
        var author = await UserAsync(db, clock, 30, "ana@situ.test", "ana", "+573001111111");
        var category = Category("accidente");
        db.ReportCategories.Add(category);
        await db.SaveChangesAsync();
        var before = counting.Calls;

        db.Reports.Add(Report.Create(author.Id, category.Id, author.CityId, new GeoPoint(4.711, -74.072), "Hay un choque.", false, false, TimeSpan.FromHours(1), Now));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());

        Assert.Equal(before, counting.Calls);
        Assert.Empty(db.MapZoneNotices.Local);
    }

    [Fact]
    public void Subscription_limits_close_the_connection_without_keeping_the_cells()
    {
        var book = new MapSubscriptionBook();
        var limits = new MapSubscriptionLimits(0.05, 16, 20);
        book.Connect("c1");
        var now = Now;

        var invalid = book.Subscribe("c1", ["nope"], now, limits);
        Assert.True(invalid.Close);
        Assert.Equal("invalid_cell", invalid.Reason);
        Assert.Equal(0, book.SubscribedCellCount);

        var tooMany = book.Subscribe("c1", Enumerable.Range(0, 17).Select(index => $"{index}:0").ToArray(), now.AddSeconds(1), limits);
        Assert.Equal("too_many_cells", tooMany.Reason);
        Assert.Equal(0, book.SubscribedCellCount);

        var accepted = book.Subscribe("c1", ["0:0", "1:0"], now.AddSeconds(2), limits);
        Assert.False(accepted.Close);
        Assert.Equal(2, book.SubscribedCellCount);

        for (var call = 0; call < 18; call++)
        {
            var decision = book.Subscribe("c1", ["0:0"], now.AddSeconds(3 + call), limits);
            if (call == 17)
            {
                Assert.Equal("subscribe_rate", decision.Reason);
            }
            else
            {
                Assert.False(decision.Close);
            }
        }

        book.Disconnect("c1");
        Assert.Equal(0, book.ConnectionCount);
        Assert.Equal(0, book.SubscribedCellCount);
    }

    [Fact]
    public async Task Staff_missing_device_and_revoked_device_are_rejected()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var user = await UserAsync(db, clock, 30, "ana@situ.test", "ana", "+573001111111");
        var device = UserDevice.Register(user.Id, "phone", DevicePlatform.IOS, "iPhone", "1", null, Now);
        db.UserDevices.Add(device);
        await db.SaveChangesAsync();
        var handler = new AuthorizeMapConnectionHandler(db, clock);
        var expires = Now.AddMinutes(15);

        var staff = await handler.Handle(new AuthorizeMapConnectionRequest(user.Id, device.Id, true, expires), CancellationToken.None);
        Assert.Equal("map.staff_forbidden", staff.Error!.Code);

        var missing = await handler.Handle(new AuthorizeMapConnectionRequest(user.Id, null, false, expires), CancellationToken.None);
        Assert.Equal("map.device_required", missing.Error!.Code);

        device.Revoke(Now);
        await db.SaveChangesAsync();
        var revoked = await handler.Handle(new AuthorizeMapConnectionRequest(user.Id, device.Id, false, expires), CancellationToken.None);
        Assert.Equal("map.device_revoked", revoked.Error!.Code);
    }

    [Fact]
    public async Task Batch_hides_what_this_viewer_cannot_see_and_keeps_anonymous_authors_hidden()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var author = await UserAsync(db, clock, 30, "ana@situ.test", "ana", "+573001111111");
        var viewer = await UserAsync(db, clock, 30, "diego@situ.test", "diego", "+573004444444");
        var stranger = await UserAsync(db, clock, 30, "bruno@situ.test", "bruno", "+573002222222");
        var minor = await UserAsync(db, clock, 16, "carla@situ.test", "carla", "+573003333333");
        var open = Category("accidente");
        var adult = Category("solo_adultos", adultOnly: true);
        db.ReportCategories.AddRange(open, adult);
        await db.SaveChangesAsync();
        var visible = Report.Create(author.Id, open.Id, author.CityId, new GeoPoint(4.711, -74.072), "Público.", true, false, TimeSpan.FromHours(2), Now);
        var friends = Report.Create(author.Id, open.Id, author.CityId, new GeoPoint(4.712, -74.073), "Amigos.", false, false, TimeSpan.FromHours(2), Now, visibility: ContentVisibility.FriendsOnly);
        var adultReport = Report.Create(author.Id, adult.Id, author.CityId, new GeoPoint(4.713, -74.074), "Adultos.", true, false, TimeSpan.FromHours(2), Now);
        var blocked = Report.Create(author.Id, open.Id, author.CityId, new GeoPoint(4.714, -74.075), "Bloqueado.", false, false, TimeSpan.FromHours(2), Now);
        db.Reports.AddRange(visible, friends, adultReport, blocked);
        db.UserBlocks.Add(UserBlock.Create(stranger.Id, author.Id, Now));
        await db.SaveChangesAsync();
        var handler = new GetReportBatchHandler(db, clock, new FakeSigner());

        var forViewer = await handler.Handle(
            new GetReportBatchRequest(viewer.Id, [visible.Id, friends.Id, adultReport.Id, blocked.Id], null, null),
            CancellationToken.None);
        Assert.Equal(3, forViewer.Value!.Items.Count);
        Assert.DoesNotContain(forViewer.Value.Items, item => item.Id == friends.Id);
        var anonymous = Assert.Single(forViewer.Value.Items, item => item.Id == visible.Id);
        Assert.Null(anonymous.Author!.UserId);
        Assert.True(anonymous.HideAuthorOnMap);

        var forStranger = await handler.Handle(
            new GetReportBatchRequest(stranger.Id, [visible.Id, blocked.Id], null, null),
            CancellationToken.None);
        Assert.Empty(forStranger.Value!.Items);

        var forMinor = await handler.Handle(new GetReportBatchRequest(minor.Id, [adultReport.Id, visible.Id], null, null), CancellationToken.None);
        Assert.Equal(visible.Id, Assert.Single(forMinor.Value!.Items).Id);

        var tooMany = await handler.Handle(
            new GetReportBatchRequest(stranger.Id, Enumerable.Range(0, 51).Select(_ => Guid.CreateVersion7()).ToArray(), null, null),
            CancellationToken.None);
        Assert.Equal("report.batch_too_large", tooMany.Error!.Code);
    }

    private static ReportCategory Category(string code, bool adultOnly = false)
    {
        var category = ReportCategory.Create(code, code, "Descripción de la categoría de prueba.", "/icon.png", "/map.png", "#112233", 1, 90, 5, isAdultOnly: adultOnly);
        category.MarkCreated(Now);
        return category;
    }

    private static async Task<User> UserAsync(IdentityTestDb db, FixedClock clock, int years, string email, string userName, string phone)
    {
        if (!await db.Cities.AnyAsync())
        {
            var country = Country.Create("CO", "Colombia", "Departamento", "es-CO", "COP", "America/Bogota", DistanceUnit.Kilometers, 14, true);
            var region = Region.Create(country.Id, "Cundinamarca", "25");
            var city = City.Create(region.Id, country.Id, "Bogotá", new GeoPoint(4.711, -74.072), "America/Bogota");
            db.AddRange(country, region, city);
            await db.SaveChangesAsync();
        }

        var cityId = await db.Cities.Select(item => item.Id).FirstAsync();
        var regionId = await db.Regions.Select(item => item.Id).FirstAsync();
        var countryId = await db.Countries.Select(item => item.Id).FirstAsync();
        var user = User.Register(
            Email.Create(email),
            UserName.Create(userName),
            PersonName.Create("Ana"),
            PersonName.Create("Pérez"),
            PhoneNumber.Create(phone),
            "hash",
            DateOnly.FromDateTime(clock.UtcNow.UtcDateTime).AddYears(-years),
            countryId,
            regionId,
            cityId,
            "es-CO",
            14,
            clock.UtcNow);
        user.ConfirmEmail(clock.UtcNow);
        if (years < 18)
        {
            user.GrantGuardianConsent(clock.UtcNow);
        }

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private sealed class ThrowingPublisher : IMapZonePublisher
    {
        public Task PublishAsync(string cellId, IReadOnlyList<MapZoneSignal> events, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("hub down");
    }

    private sealed class RememberingPublisher : IMapZonePublisher
    {
        public List<IReadOnlyList<MapZoneSignal>> Batches { get; } = [];

        public Task PublishAsync(string cellId, IReadOnlyList<MapZoneSignal> events, CancellationToken cancellationToken)
        {
            Batches.Add(events);
            return Task.CompletedTask;
        }
    }

    private sealed class CountingDispatcher : IDomainEventDispatcher
    {
        public int Calls { get; private set; }

        public Task DispatchAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowWhenReportIsAdded : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            if (AddsReport(eventData.Context))
            {
                throw new InvalidOperationException("save failed");
            }

            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (AddsReport(eventData.Context))
            {
                throw new InvalidOperationException("save failed");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private static bool AddsReport(DbContext? context) =>
            context?.ChangeTracker.Entries<Report>().Any(entry => entry.State == EntityState.Added) == true;
    }

    private sealed class QuietLogger : ILogger<DomainEventDispatcher>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }
    }

    private sealed class RootScope(IApplicationDbContext db, IClock clock) : IServiceScopeFactory, IServiceScope, IServiceProvider
    {
        public IServiceProvider ServiceProvider => this;

        public IServiceScope CreateScope() => this;

        public void Dispose()
        {
        }

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IApplicationDbContext))
            {
                return db;
            }

            if (serviceType == typeof(IClock))
            {
                return clock;
            }

            return null;
        }
    }

    private sealed class FakeSigner : IMediaUrlSigner
    {
        public (long ExpiresUnix, string Signature) Sign(Guid mediaId, DateTimeOffset expiresAt) => (expiresAt.ToUnixTimeSeconds(), "sig");

        public bool IsValid(Guid mediaId, long expiresUnix, string signature, DateTimeOffset now) => signature == "sig";
    }
}
