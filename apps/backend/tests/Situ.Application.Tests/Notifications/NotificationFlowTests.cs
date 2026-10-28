using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Features.Identity.SignOut;
using Situ.Application.Features.Reports.GetReport;
using Situ.Application.Features.Notifications;
using Situ.Application.Features.Notifications.Devices;
using Situ.Application.Features.Notifications.Dispatch;
using Situ.Application.Features.Notifications.Inbox;
using Situ.Application.Features.Notifications.PurgeAlertLocations;
using Situ.Application.Features.Notifications.Settings;
using Situ.Application.Tests.Support;
using Situ.Domain.Administration;
using Situ.Domain.Common;
using Situ.Domain.Geography;
using Situ.Domain.Guardianship;
using Situ.Domain.Identity;
using Situ.Domain.Live;
using Situ.Domain.Notifications;
using Situ.Domain.Reports;
using Situ.Domain.Social;

namespace Situ.Application.Tests.Notifications;

public class NotificationFlowTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_report_reaches_the_interest_city_an_extra_city_and_only_the_nearby_device()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var catalog = await CatalogAsync(db);
        var category = Category("hurto", "Hurto");
        db.ReportCategories.Add(category);
        var author = await UserAsync(db, catalog, 30, "autor@situ.test", "autor", "+573000000001");
        var home = await UserAsync(db, catalog, 30, "casa@situ.test", "casa", "+573000000002");
        var extra = await UserAsync(db, catalog, 30, "otra@situ.test", "otra", "+573000000003", catalog.OtherCityId);
        var near = await UserAsync(db, catalog, 30, "cerca@situ.test", "cerca", "+573000000004", catalog.OtherCityId);
        var far = await UserAsync(db, catalog, 30, "lejos@situ.test", "lejos", "+573000000005", catalog.OtherCityId);
        db.UserInterestCities.Add(UserInterestCity.Create(extra.Id, catalog.CityId));
        await PlaceAsync(db, near, 4.715, -74.072);
        await PlaceAsync(db, far, 4.800, -74.072);
        var report = await ReportAsync(db, author, category.Id, false, ContentVisibility.Public);
        var push = new RecordingPush();

        await DispatchAsync(db, clock, push, NotificationJob.ReportCreated, report.Id);

        var notes = await db.Notifications.OrderBy(item => item.RecipientId).ToListAsync();
        Assert.Equal(3, notes.Count);
        Assert.Contains(notes, item => item.RecipientId == home.Id && item.Body.Contains("Bogotá", StringComparison.Ordinal));
        Assert.Contains(notes, item => item.RecipientId == extra.Id && item.Body.Contains("Hurto", StringComparison.Ordinal));
        Assert.Contains(notes, item => item.RecipientId == near.Id && item.Body.Contains("Hurto a", StringComparison.Ordinal));
        Assert.DoesNotContain(notes, item => item.RecipientId == far.Id || item.RecipientId == author.Id);
        Assert.Equal(3, push.Notes.Count);
        Assert.All(push.Notes, note =>
        {
            Assert.Equal(report.Id.ToString(), note.Payload.ReportId);
            Assert.Equal(nameof(NotificationType.NearbyReport), note.Payload.Type);
            Assert.Equal("Report", note.Payload.TargetType);
            Assert.Equal(1, note.Badge);
            AssertOpaque(note, author);
        });
    }

    [Fact]
    public async Task One_user_gets_a_single_friend_notification_when_several_triggers_match()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var catalog = await CatalogAsync(db);
        var category = Category("accidente", "Accidente de tránsito");
        db.ReportCategories.Add(category);
        var author = await UserAsync(db, catalog, 30, "autor2@situ.test", "autor2", "+573000000011");
        var friend = await UserAsync(db, catalog, 30, "amigo@situ.test", "amigo", "+573000000012");
        await PlaceAsync(db, friend, 4.715, -74.072);
        var friendship = Friendship.Request(author.Id, friend.Id, Now);
        friendship.Accept(friend.Id, Now);
        db.Friendships.Add(friendship);
        var report = await ReportAsync(db, author, category.Id, false, ContentVisibility.Public);
        var push = new RecordingPush();

        await DispatchAsync(db, clock, push, NotificationJob.ReportCreated, report.Id);

        var note = Assert.Single(await db.Notifications.ToListAsync());
        Assert.Equal(friend.Id, note.RecipientId);
        Assert.Equal(NotificationType.FriendReport, note.Type);
        Assert.Equal("Un amigo reportó: Accidente de tránsito", note.Body);
        var sent = Assert.Single(push.Notes);
        Assert.Equal(nameof(NotificationType.FriendReport), sent.Payload.Type);
        Assert.Equal(report.Id.ToString(), sent.Payload.ReportId);
        AssertOpaque(sent, author);
    }

    [Fact]
    public async Task An_anonymous_report_notifies_city_and_proximity_but_never_a_friend_or_the_author()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var catalog = await CatalogAsync(db);
        var category = Category("basura", "Basura");
        db.ReportCategories.Add(category);
        var author = await UserAsync(db, catalog, 30, "autor3@situ.test", "autor3", "+573000000021");
        var friend = await UserAsync(db, catalog, 30, "amigo3@situ.test", "amigo3", "+573000000022");
        var friendship = Friendship.Request(author.Id, friend.Id, Now);
        friendship.Accept(friend.Id, Now);
        db.Friendships.Add(friendship);
        var report = await ReportAsync(db, author, category.Id, true, ContentVisibility.Public);
        var push = new RecordingPush();

        await DispatchAsync(db, clock, push, NotificationJob.ReportCreated, report.Id);

        var note = Assert.Single(await db.Notifications.ToListAsync());
        Assert.Equal(friend.Id, note.RecipientId);
        Assert.Equal(NotificationType.NearbyReport, note.Type);
        Assert.DoesNotContain("autor3", note.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(author.Id.ToString(), note.Body, StringComparison.OrdinalIgnoreCase);
        AssertOpaque(Assert.Single(push.Notes), author);
    }

    [Fact]
    public async Task Exclusions_drop_the_author_blocks_muted_adult_categories_and_closed_accounts()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var catalog = await CatalogAsync(db);
        var category = Category("adulto", "Solo adultos", adult: true);
        db.ReportCategories.Add(category);
        var author = await UserAsync(db, catalog, 30, "autor4@situ.test", "autor4", "+573000000031");
        var blocked = await UserAsync(db, catalog, 30, "bloqueo@situ.test", "bloqueo", "+573000000032");
        var anonBlocker = await UserAsync(db, catalog, 30, "anonb@situ.test", "anonb", "+573000000033");
        var muted = await UserAsync(db, catalog, 30, "silencio@situ.test", "silencio", "+573000000034");
        var minor = await UserAsync(db, catalog, 16, "menor@situ.test", "menor", "+573000000035");
        var off = await UserAsync(db, catalog, 30, "apagado@situ.test", "apagado", "+573000000036");
        var suspended = await UserAsync(db, catalog, 30, "suspendido@situ.test", "suspendido", "+573000000037");
        var named = await UserAsync(db, catalog, 30, "publico@situ.test", "publico", "+573000000038");
        db.UserBlocks.Add(UserBlock.CreateFull(blocked.Id, author.Id, Now));
        db.UserBlocks.Add(UserBlock.HideAnonymousContent(anonBlocker.Id, author.Id, ContentTargetType.Report, Guid.NewGuid(), Now, Now));
        db.UserCategoryInterests.Add(UserCategoryInterest.Create(muted.Id, category.Id, true));
        var settings = NotificationSettings.CreateDefault(off.Id, "America/Bogota", Now);
        settings.Update(true, true, 1000, false, true, true, true, true, null, null, "America/Bogota", Now);
        db.NotificationSettings.Add(settings);
        suspended.Suspend(Now.AddDays(1));
        await db.SaveChangesAsync();
        var report = await ReportAsync(db, author, category.Id, true, ContentVisibility.Public);

        await DispatchAsync(db, clock, new RecordingPush(), NotificationJob.ReportCreated, report.Id);

        var recipients = await db.Notifications.Select(item => item.RecipientId).ToListAsync();
        Assert.DoesNotContain(author.Id, recipients);
        Assert.DoesNotContain(blocked.Id, recipients);
        Assert.DoesNotContain(anonBlocker.Id, recipients);
        Assert.DoesNotContain(muted.Id, recipients);
        Assert.DoesNotContain(minor.Id, recipients);
        Assert.DoesNotContain(off.Id, recipients);
        Assert.DoesNotContain(suspended.Id, recipients);
        Assert.Contains(named.Id, recipients);
    }

    [Fact]
    public async Task An_anonymous_block_still_allows_a_named_report()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var catalog = await CatalogAsync(db);
        var category = Category("hurto2", "Hurto");
        db.ReportCategories.Add(category);
        var author = await UserAsync(db, catalog, 30, "autor5@situ.test", "autor5", "+573000000041");
        var blocker = await UserAsync(db, catalog, 30, "viewer@situ.test", "viewer", "+573000000042");
        db.UserBlocks.Add(UserBlock.HideAnonymousContent(blocker.Id, author.Id, ContentTargetType.Report, Guid.NewGuid(), Now, Now));
        var report = await ReportAsync(db, author, category.Id, false, ContentVisibility.Public);

        await DispatchAsync(db, clock, new RecordingPush(), NotificationJob.ReportCreated, report.Id);

        Assert.Contains(await db.Notifications.ToListAsync(), item => item.RecipientId == blocker.Id);
    }

    [Fact]
    public async Task A_minor_without_location_consent_keeps_city_alerts_and_cannot_store_a_proximity_point()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var catalog = await CatalogAsync(db);
        var category = Category("retencion", "Retén");
        db.ReportCategories.Add(category);
        var author = await UserAsync(db, catalog, 30, "autor6@situ.test", "autor6", "+573000000051");
        var minor = await UserAsync(db, catalog, 16, "menor6@situ.test", "menor6", "+573000000052");
        var device = UserDevice.Register(minor.Id, "phone", DevicePlatform.IOS, null, null, null, Now);
        db.UserDevices.Add(device);
        await db.SaveChangesAsync();
        var denied = await new SetAlertLocationHandler(db, clock).Handle(
            new SetAlertLocationRequest(minor.Id, device.Id, 4.71149, -74.07249, 12),
            CancellationToken.None);
        Assert.Equal("notification.proximity_not_allowed_minor", denied.Error!.Code);
        Assert.Empty(db.DeviceAlertLocations);

        var consent = GuardianConsent.Request(minor.Id, "tutor@situ.test", "hash", "v1", Now, Now);
        consent.SubmitGuardianData("Carlos Pérez", GuardianDocumentType.Passport, "P123", "carlos@correo.com", Now);
        consent.VerifyGuardianEmail(Now);
        consent.Authorize("signature", false, true, null, null, Now);
        db.GuardianConsents.Add(consent);
        await db.SaveChangesAsync();
        var allowed = await new SetAlertLocationHandler(db, clock).Handle(
            new SetAlertLocationRequest(minor.Id, device.Id, 4.71149, -74.07249, 12),
            CancellationToken.None);
        Assert.True(allowed.IsSuccess);
        var stored = Assert.Single(db.DeviceAlertLocations);
        Assert.Equal(4.711, stored.Location.Latitude);
        Assert.Equal(-74.072, stored.Location.Longitude);
        Assert.DoesNotContain("4.71149", stored.Location.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

        var report = await ReportAsync(db, author, category.Id, false, ContentVisibility.Public);
        await DispatchAsync(db, clock, new RecordingPush(), NotificationJob.ReportCreated, report.Id);
        Assert.Contains(await db.Notifications.ToListAsync(), item => item.RecipientId == minor.Id);
    }

    [Fact]
    public async Task Quiet_hours_follow_the_user_zone_and_the_hourly_limit_collapses_into_one_summary()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var catalog = await CatalogAsync(db);
        var category = Category("trafico", "Tráfico");
        db.ReportCategories.Add(category);
        var author = await UserAsync(db, catalog, 30, "autor7@situ.test", "autor7", "+573000000061");
        var quiet = await UserAsync(db, catalog, 30, "silencio7@situ.test", "silencio7", "+573000000062");
        var awake = await UserAsync(db, catalog, 30, "despierto@situ.test", "despierto", "+573000000063", catalog.OtherCityId);
        var quietSettings = NotificationSettings.CreateDefault(quiet.Id, "America/Bogota", Now);
        quietSettings.Update(true, true, 1000, true, true, true, true, true, new TimeOnly(9, 0), new TimeOnly(11, 0), "America/Bogota", Now);
        var awakeSettings = NotificationSettings.CreateDefault(awake.Id, "America/New_York", Now);
        awakeSettings.Update(true, true, 1000, true, true, true, true, true, new TimeOnly(9, 0), new TimeOnly(11, 0), "America/New_York", Now);
        db.UserInterestCities.Add(UserInterestCity.Create(awake.Id, catalog.CityId));
        db.NotificationSettings.AddRange(quietSettings, awakeSettings);
        var report = await ReportAsync(db, author, category.Id, false, ContentVisibility.Public);
        var push = new RecordingPush();

        await DispatchAsync(db, clock, push, NotificationJob.ReportCreated, report.Id);

        Assert.Contains(await db.NotificationDeliveries.ToListAsync(), item => item.Status == DeliveryStatus.Suppressed && item.Error == "quiet_hours");
        var quietDevice = await db.UserDevices.SingleAsync(item => item.UserId == quiet.Id);
        Assert.Contains(push.Notes, item => item.Title == "En tu ciudad");
        Assert.DoesNotContain(push.Notes, item => item.DeviceId == quietDevice.Id);

        for (var index = 0; index < 6; index++)
        {
            var filler = Notification.Create(awake.Id, NotificationType.NearbyReport, "En tu ciudad", "previo", ContentTargetType.Report, Guid.NewGuid(), Now);
            db.Notifications.Add(filler);
            var delivery = NotificationDelivery.Create(filler.Id, NotificationChannel.Push, null, Now);
            delivery.MarkSent(Now);
            db.NotificationDeliveries.Add(delivery);
        }

        await db.SaveChangesAsync();
        var second = await ReportAsync(db, author, category.Id, false, ContentVisibility.Public, 4.720, -74.072);
        push.Notes.Clear();
        await DispatchAsync(db, clock, push, NotificationJob.ReportCreated, second.Id);

        Assert.Contains(await db.Notifications.ToListAsync(), item => item.RecipientId == awake.Id && item.GroupKey != null && item.Body.Contains("reportes nuevos", StringComparison.Ordinal));
        Assert.Contains(await db.NotificationDeliveries.ToListAsync(), item => item.Error == "hourly_limit");
        var summary = Assert.Single(push.Notes);
        Assert.Equal("En tu zona", summary.Title);
        Assert.Equal("Summary", summary.Payload.Type);
        Assert.Null(summary.Payload.ReportId);
        Assert.Null(summary.Payload.TargetId);
        var unread = await db.Notifications.CountAsync(item => item.RecipientId == awake.Id && item.ReadAt == null);
        Assert.Equal(unread, summary.Badge);
        AssertOpaque(summary, author);
    }

    [Fact]
    public async Task Alert_location_is_rounded_rate_limited_purged_and_removed_on_sign_out()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var catalog = await CatalogAsync(db);
        var user = await UserAsync(db, catalog, 30, "punto@situ.test", "punto", "+573000000071");
        var device = await db.UserDevices.SingleAsync(item => item.UserId == user.Id);
        device.UpdatePushToken("ExponentPushToken[abc]");
        db.RefreshTokens.Add(RefreshToken.Issue(user.Id, device.Id, "refresh-plain", Now, TimeSpan.FromDays(30)));
        await db.SaveChangesAsync();
        var handler = new SetAlertLocationHandler(db, clock);

        var saved = await handler.Handle(new SetAlertLocationRequest(user.Id, device.Id, 4.7115, -74.0724, 20), CancellationToken.None);
        Assert.True(saved.IsSuccess);
        Assert.Null(saved.Error);
        var point = Assert.Single(db.DeviceAlertLocations);
        Assert.Equal(4.712, point.Location.Latitude);
        Assert.Equal(-74.072, point.Location.Longitude);

        var limited = await handler.Handle(new SetAlertLocationRequest(user.Id, device.Id, 4.720, -74.080, 20), CancellationToken.None);
        Assert.Equal("notification.alert_location_limited", limited.Error!.Code);

        clock.UtcNow = Now.AddMinutes(31);
        var purged = await new PurgeAlertLocationsHandler(db, clock).Handle(new PurgeAlertLocationsRequest(), CancellationToken.None);
        Assert.Equal(1, purged.Value!.Removed);
        Assert.Empty(db.DeviceAlertLocations);

        clock.UtcNow = Now.AddMinutes(40);
        await handler.Handle(new SetAlertLocationRequest(user.Id, device.Id, 4.711, -74.072, 10), CancellationToken.None);
        await new SignOutHandler(db, clock, new EchoTokens()).Handle(new SignOutRequest("refresh-plain"), CancellationToken.None);
        Assert.Empty(db.DeviceAlertLocations);
        Assert.Null((await db.UserDevices.SingleAsync()).PushToken);
    }

    [Fact]
    public async Task Clearing_the_alert_location_is_idempotent_and_leaves_the_token_and_other_devices()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var catalog = await CatalogAsync(db);
        var user = await UserAsync(db, catalog, 30, "borrar@situ.test", "borrar", "+573000000171");
        var current = await db.UserDevices.SingleAsync(item => item.UserId == user.Id);
        current.UpdatePushToken("ExponentPushToken[current]");
        var other = UserDevice.Register(user.Id, "other-phone", DevicePlatform.Android, null, null, null, Now);
        other.UpdatePushToken("ExponentPushToken[other]");
        db.UserDevices.Add(other);
        db.DeviceAlertLocations.Add(DeviceAlertLocation.Record(user.Id, current.Id, 4.711, -74.072, 20, Now, TimeSpan.FromMinutes(30)));
        db.DeviceAlertLocations.Add(DeviceAlertLocation.Record(user.Id, other.Id, 4.720, -74.080, 20, Now, TimeSpan.FromMinutes(30)));
        await db.SaveChangesAsync();
        var handler = new ClearAlertLocationHandler(db);

        var first = await handler.Handle(new ClearAlertLocationRequest(user.Id, current.Id), CancellationToken.None);
        var second = await handler.Handle(new ClearAlertLocationRequest(user.Id, current.Id), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        var remaining = Assert.Single(db.DeviceAlertLocations);
        Assert.Equal(other.Id, remaining.DeviceId);
        Assert.Equal("ExponentPushToken[current]", (await db.UserDevices.SingleAsync(item => item.Id == current.Id)).PushToken);
    }

    [Fact]
    public async Task An_unavailable_report_uses_a_stable_code_and_the_author_can_still_open_it()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var catalog = await CatalogAsync(db);
        var author = await UserAsync(db, catalog, 30, "dueno@situ.test", "dueno", "+573000000181");
        var viewer = await UserAsync(db, catalog, 30, "visita@situ.test", "visita", "+573000000182");
        var expired = await ReportAsync(db, author, Guid.NewGuid(), false, ContentVisibility.Public);
        var removed = await ReportAsync(db, author, Guid.NewGuid(), false, ContentVisibility.Public, 4.72, -74.08);
        var review = await ReportAsync(db, author, Guid.NewGuid(), true, ContentVisibility.Public, 4.73, -74.09);
        var lapsed = await ReportAsync(db, author, Guid.NewGuid(), false, ContentVisibility.Public, 4.74, -74.1);
        expired.Expire(Now);
        removed.Remove(Now);
        review.SendToReview(Now);
        await db.SaveChangesAsync();
        var handler = new GetReportHandler(db, clock, new Signer());

        await AssertUnavailable(handler, viewer.Id, expired.Id, "Expired");
        await AssertUnavailable(handler, viewer.Id, removed.Id, "Removed");
        await AssertUnavailable(handler, viewer.Id, review.Id, "UnderReview");
        clock.UtcNow = lapsed.ExpiresAt.AddMinutes(1);
        await AssertUnavailable(handler, viewer.Id, lapsed.Id, "Expired");

        var asAuthor = await handler.Handle(new GetReportRequest(author.Id, expired.Id), CancellationToken.None);
        Assert.True(asAuthor.IsSuccess);
        var missing = await handler.Handle(new GetReportRequest(viewer.Id, Guid.NewGuid()), CancellationToken.None);
        Assert.Equal("report.not_found", missing.Error!.Code);
    }

    [Fact]
    public async Task A_rejected_push_token_is_cleared_and_a_retried_job_does_not_duplicate_the_inbox()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var catalog = await CatalogAsync(db);
        var category = Category("choque", "Choque");
        db.ReportCategories.Add(category);
        var author = await UserAsync(db, catalog, 30, "autor8@situ.test", "autor8", "+573000000081");
        var reader = await UserAsync(db, catalog, 30, "lector@situ.test", "lector", "+573000000082");
        var report = await ReportAsync(db, author, category.Id, false, ContentVisibility.Public);
        var push = new RecordingPush { Reject = true };

        await DispatchAsync(db, clock, push, NotificationJob.ReportCreated, report.Id);

        Assert.Null((await db.UserDevices.SingleAsync(item => item.UserId == reader.Id)).PushToken);
        Assert.Contains(await db.NotificationDeliveries.ToListAsync(), item => item.Status == DeliveryStatus.Failed && item.Error == "DeviceNotRegistered");

        db.NotificationJobs.RemoveRange(await db.NotificationJobs.ToListAsync());
        await db.SaveChangesAsync();
        await DispatchAsync(db, clock, push, NotificationJob.ReportCreated, report.Id);
        Assert.Single(await db.Notifications.Where(item => item.TargetId == report.Id).ToListAsync());
    }

    [Fact]
    public async Task An_anonymous_comment_notifies_the_report_author_without_naming_anyone()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var catalog = await CatalogAsync(db);
        var author = await UserAsync(db, catalog, 30, "autor9@situ.test", "autor9", "+573000000091");
        var commenter = await UserAsync(db, catalog, 30, "comenta@situ.test", "comenta", "+573000000092");
        var report = await ReportAsync(db, author, Guid.NewGuid(), false, ContentVisibility.Public);
        var comment = Comment.Create(ContentTargetType.Report, report.Id, commenter.Id, "Vi lo mismo en la esquina", true, 1, null, author.Id, Now);
        db.Comments.Add(comment);
        await db.SaveChangesAsync();

        var push = new RecordingPush();
        await DispatchAsync(db, clock, push, NotificationJob.CommentAdded, comment.Id);

        var note = Assert.Single(await db.Notifications.ToListAsync());
        Assert.Equal(author.Id, note.RecipientId);
        Assert.Equal("Alguien comentó tu reporte.", note.Body);
        Assert.DoesNotContain("comenta", note.Body, StringComparison.OrdinalIgnoreCase);
        var sent = Assert.Single(push.Notes);
        Assert.Equal(nameof(NotificationType.Comment), sent.Payload.Type);
        Assert.Equal("Comment", sent.Payload.TargetType);
        Assert.Equal(comment.Id.ToString(), sent.Payload.TargetId);
        Assert.Equal(report.Id.ToString(), sent.Payload.ReportId);
        Assert.DoesNotContain("Vi lo mismo", sent.Body + JsonSerializer.Serialize(sent.Payload), StringComparison.Ordinal);
        AssertOpaque(sent, commenter);
        var listed = await new ListNotificationsHandler(db, clock).Handle(new ListNotificationsRequest(author.Id, null, 20), CancellationToken.None);
        var item = Assert.Single(listed.Value!.Notifications);
        Assert.Equal(report.Id, item.ReportId);
        Assert.True(item.Available);
        Assert.DoesNotContain("comenta", item.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Friends_only_live_notifies_friends_and_skips_everyone_else()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var catalog = await CatalogAsync(db);
        var streamer = await UserAsync(db, catalog, 30, "emite@situ.test", "emite", "+573000000101");
        var friend = await UserAsync(db, catalog, 30, "amigol@situ.test", "amigol", "+573000000102");
        var near = await UserAsync(db, catalog, 30, "cercal@situ.test", "cercal", "+573000000103", catalog.OtherCityId);
        var blocked = await UserAsync(db, catalog, 30, "bloqueol@situ.test", "bloqueol", "+573000000104");
        var quiet = await UserAsync(db, catalog, 30, "silenciol@situ.test", "silenciol", "+573000000105");
        var off = await UserAsync(db, catalog, 30, "apagadol@situ.test", "apagadol", "+573000000106");
        foreach (var person in new[] { friend, blocked, quiet, off })
        {
            var tie = Friendship.Request(streamer.Id, person.Id, Now);
            tie.Accept(person.Id, Now);
            db.Friendships.Add(tie);
        }
        db.UserBlocks.Add(UserBlock.CreateFull(blocked.Id, streamer.Id, Now));
        var quietSettings = NotificationSettings.CreateDefault(quiet.Id, "America/Bogota", Now);
        quietSettings.Update(true, true, 1000, true, true, true, true, true, new TimeOnly(9, 0), new TimeOnly(11, 0), "America/Bogota", Now);
        var offSettings = NotificationSettings.CreateDefault(off.Id, "America/Bogota", Now);
        offSettings.Update(true, true, 1000, true, true, false, true, true, null, null, "America/Bogota", Now);
        db.NotificationSettings.AddRange(quietSettings, offSettings);
        await PlaceAsync(db, near, 4.715, -74.072);
        db.UserInterestCities.Add(UserInterestCity.Create(near.Id, catalog.CityId));
        var stream = await StreamAsync(db, streamer, ContentVisibility.FriendsOnly, new GeoPoint(4.711, -74.072), catalog.CityId);
        var push = new RecordingPush();

        await DispatchAsync(db, clock, push, NotificationJob.LiveStarted, stream.Id);
        await DispatchAsync(db, clock, push, NotificationJob.LiveStarted, stream.Id);

        var notes = await db.Notifications.ToListAsync();
        Assert.Equal(2, notes.Count);
        Assert.Contains(notes, item => item.RecipientId == friend.Id && item.Type == NotificationType.FriendLiveStarted && item.Body == "Un amigo está en vivo.");
        Assert.Contains(notes, item => item.RecipientId == quiet.Id && item.Body == "Un amigo está en vivo.");
        Assert.DoesNotContain(notes, item => item.RecipientId == streamer.Id || item.RecipientId == near.Id || item.RecipientId == blocked.Id || item.RecipientId == off.Id);
        var sent = Assert.Single(push.Notes);
        Assert.Equal(stream.PublicPath, sent.Payload.TargetId);
        Assert.Equal(nameof(NotificationType.FriendLiveStarted), sent.Payload.Type);
        Assert.DoesNotContain(streamer.UserName.Value, sent.Body, StringComparison.OrdinalIgnoreCase);
        AssertOpaque(sent, streamer);
        var quietNoteId = notes.Single(note => note.RecipientId == quiet.Id).Id;
        var quietDelivery = await db.NotificationDeliveries.SingleAsync(item => item.NotificationId == quietNoteId);
        Assert.Equal(DeliveryStatus.Suppressed, quietDelivery.Status);
        Assert.Equal("quiet_hours", quietDelivery.Error);
    }

    [Fact]
    public async Task A_public_live_notifies_nearby_and_interest_city_once_and_skips_a_stream_without_a_place()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var catalog = await CatalogAsync(db);
        var streamer = await UserAsync(db, catalog, 30, "publico@situ.test", "publico", "+573000000111");
        var near = await UserAsync(db, catalog, 30, "cerca2@situ.test", "cerca2", "+573000000112", catalog.OtherCityId);
        var city = await UserAsync(db, catalog, 30, "ciudad2@situ.test", "ciudad2", "+573000000113", catalog.OtherCityId);
        var both = await UserAsync(db, catalog, 30, "ambos@situ.test", "ambos", "+573000000114", catalog.OtherCityId);
        var friend = await UserAsync(db, catalog, 30, "lejano@situ.test", "lejano", "+573000000115");
        var minor = await UserAsync(db, catalog, 16, "menorv@situ.test", "menorv", "+573000000116", catalog.OtherCityId);
        var off = await UserAsync(db, catalog, 30, "offlive@situ.test", "offlive", "+573000000117", catalog.OtherCityId);
        var tie = Friendship.Request(streamer.Id, friend.Id, Now);
        tie.Accept(friend.Id, Now);
        db.Friendships.Add(tie);
        db.UserInterestCities.Add(UserInterestCity.Create(city.Id, catalog.CityId));
        db.UserInterestCities.Add(UserInterestCity.Create(both.Id, catalog.CityId));
        await PlaceAsync(db, near, 4.715, -74.072);
        await PlaceAsync(db, both, 4.714, -74.072);
        await PlaceAsync(db, minor, 4.715, -74.072);
        await PlaceAsync(db, off, 4.715, -74.072);
        await PlaceAsync(db, streamer, 4.711, -74.072);
        var offSettings = NotificationSettings.CreateDefault(off.Id, "America/Bogota", Now);
        offSettings.Update(true, true, 1000, true, true, true, false, true, null, null, "America/Bogota", Now);
        db.NotificationSettings.Add(offSettings);
        var stream = await StreamAsync(db, streamer, ContentVisibility.Public, new GeoPoint(4.711, -74.072), catalog.CityId);
        var push = new RecordingPush();

        await DispatchAsync(db, clock, push, NotificationJob.LiveStarted, stream.Id);

        var notes = await db.Notifications.Where(item => item.Type == NotificationType.NearbyLiveStarted).ToListAsync();
        Assert.Equal(3, notes.Count);
        Assert.All(notes, item => Assert.Equal("Hay una transmisión en vivo cerca de ti.", item.Body));
        Assert.Contains(notes, item => item.RecipientId == near.Id);
        Assert.Contains(notes, item => item.RecipientId == city.Id);
        Assert.Contains(notes, item => item.RecipientId == both.Id);
        Assert.DoesNotContain(notes, item => item.RecipientId == streamer.Id || item.RecipientId == friend.Id || item.RecipientId == minor.Id || item.RecipientId == off.Id);
        Assert.Equal(3, push.Notes.Count);
        Assert.All(push.Notes, note => Assert.Equal(stream.PublicPath, note.Payload.TargetId));

        var unplaced = await StreamAsync(db, streamer, ContentVisibility.Public, null, catalog.CityId);
        await DispatchAsync(db, clock, push, NotificationJob.LiveStarted, unplaced.Id);
        Assert.Equal(3, await db.Notifications.CountAsync(item => item.Type == NotificationType.NearbyLiveStarted));
    }

    [Fact]
    public async Task A_minor_friend_hears_a_minor_stream_and_the_hourly_cap_stops_another_push()
    {
        await using var db = IdentityTestDb.Create();
        var clock = new FixedClock(Now);
        var catalog = await CatalogAsync(db);
        db.SystemSettings.Add(SystemSetting.Create(SystemSettingKeys.LiveMinorsCanStream, "true", null, Now));
        db.SystemSettings.Add(SystemSetting.Create(SystemSettingKeys.NotificationMaxPushesPerHour, "1", null, Now));
        var streamer = await UserAsync(db, catalog, 16, "menoron@situ.test", "menoron", "+573000000121");
        var friend = await UserAsync(db, catalog, 16, "menoram@situ.test", "menoram", "+573000000122");
        var adult = await UserAsync(db, catalog, 30, "adultoam@situ.test", "adultoam", "+573000000123");
        foreach (var person in new[] { friend, adult })
        {
            var tie = Friendship.Request(streamer.Id, person.Id, Now);
            tie.Accept(person.Id, Now);
            db.Friendships.Add(tie);
        }
        var prior = Notification.Create(friend.Id, NotificationType.System, "Aviso", "Ya hubo una.", null, null, Now);
        db.Notifications.Add(prior);
        await db.SaveChangesAsync();
        var device = await db.UserDevices.SingleAsync(item => item.UserId == friend.Id);
        var sent = NotificationDelivery.Create(prior.Id, NotificationChannel.Push, device.Id, Now);
        sent.MarkSent(Now);
        db.NotificationDeliveries.Add(sent);
        var stream = await StreamAsync(db, streamer, ContentVisibility.FriendsOnly, null, null);
        var push = new RecordingPush();

        await DispatchAsync(db, clock, push, NotificationJob.LiveStarted, stream.Id);

        var live = Assert.Single(await db.Notifications.Where(item => item.Type == NotificationType.FriendLiveStarted).ToListAsync());
        Assert.Equal(friend.Id, live.RecipientId);
        Assert.DoesNotContain(adult.Id, await db.Notifications.Where(item => item.Type == NotificationType.FriendLiveStarted).Select(item => item.RecipientId).ToListAsync());
        Assert.DoesNotContain(push.Notes, note => note.Body == "Un amigo está en vivo.");
        var delivery = await db.NotificationDeliveries.SingleAsync(item => item.NotificationId == live.Id);
        Assert.Equal(DeliveryStatus.Suppressed, delivery.Status);
        Assert.Equal("hourly_limit", delivery.Error);
    }

    private static async Task<LiveStream> StreamAsync(IdentityTestDb db, User streamer, ContentVisibility visibility, GeoPoint? location, Guid? cityId)
    {
        var stream = LiveStream.Schedule(
            streamer.Id,
            streamer.AgeOn(Now),
            true,
            null,
            cityId,
            location,
            "En la calle",
            visibility,
            Guid.NewGuid().ToString("N")[..16],
            "hash",
            Now.AddMinutes(5),
            Now);
        Assert.True(stream.AcceptPublishKey("hash", Now));
        Assert.True(stream.MarkLive(Now));
        db.LiveStreams.Add(stream);
        await db.SaveChangesAsync();
        return stream;
    }

    private static async Task AssertUnavailable(GetReportHandler handler, Guid viewerId, Guid reportId, string reportStatus)
    {
        var result = await handler.Handle(new GetReportRequest(viewerId, reportId), CancellationToken.None);
        Assert.Equal("report.not_available", result.Error!.Code);
        Assert.Equal("Este reporte ya no está disponible.", result.Error.Message);
        Assert.Equal(reportStatus, result.Error.Extensions["reportStatus"]);
        Assert.DoesNotContain("moder", result.Error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("denuncia", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertOpaque(PushNote note, User author)
    {
        var payload = JsonSerializer.Serialize(note.Payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var whole = payload + note.Title + note.Body;
        Assert.DoesNotContain(author.Id.ToString(), whole, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(author.UserName.Value, whole, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Ana", whole, StringComparison.Ordinal);
        Assert.DoesNotContain("Pérez", whole, StringComparison.Ordinal);
        Assert.DoesNotContain("Descripción que no debe salir", whole, StringComparison.Ordinal);
        Assert.DoesNotContain("4.711", whole, StringComparison.Ordinal);
        Assert.DoesNotContain("-74.072", whole, StringComparison.Ordinal);
        Assert.DoesNotContain("latitude", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("longitude", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("distance", payload, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task DispatchAsync(IdentityTestDb db, FixedClock clock, RecordingPush push, string kind, Guid subjectId)
    {
        db.NotificationJobs.Add(NotificationJob.Enqueue(kind, subjectId, clock.UtcNow));
        await db.SaveChangesAsync();
        await new ProcessNotificationJobsHandler(db, clock, new MemoryProximity(db), push).Handle(new ProcessNotificationJobsRequest(), CancellationToken.None);
    }

    private static async Task PlaceAsync(IdentityTestDb db, User user, double latitude, double longitude)
    {
        var device = await db.UserDevices.SingleAsync(item => item.UserId == user.Id);
        db.DeviceAlertLocations.Add(DeviceAlertLocation.Record(user.Id, device.Id, latitude, longitude, 20, Now, TimeSpan.FromMinutes(30)));
        await db.SaveChangesAsync();
    }

    private static async Task<Report> ReportAsync(
        IdentityTestDb db,
        User author,
        Guid categoryId,
        bool anonymous,
        ContentVisibility visibility,
        double latitude = 4.711,
        double longitude = -74.072)
    {
        var report = Report.Create(author.Id, categoryId, author.CityId, new GeoPoint(latitude, longitude), "Descripción que no debe salir en la alerta.", anonymous, false, TimeSpan.FromHours(2), Now, visibility: visibility);
        db.Reports.Add(report);
        await db.SaveChangesAsync();
        return report;
    }

    private static ReportCategory Category(string code, string name, bool adult = false) =>
        ReportCategory.Create(code, name, "desc", "https://cdn/b.png", "https://cdn/m.png", "#112233", 1, 60, 1, isAdultOnly: adult);

    private static async Task<User> UserAsync(IdentityTestDb db, Catalog catalog, int years, string email, string userName, string phone, Guid? cityId = null)
    {
        var user = User.Register(
            Email.Create(email),
            UserName.Create(userName),
            PersonName.Create("Ana"),
            PersonName.Create("Pérez"),
            PhoneNumber.Create(phone),
            "hash",
            DateOnly.FromDateTime(Now.UtcDateTime).AddYears(-years),
            catalog.CountryId,
            catalog.RegionId,
            cityId ?? catalog.CityId,
            "es-CO",
            14,
            Now);
        user.ConfirmEmail(Now);
        if (user.IsMinor(Now))
        {
            user.GrantGuardianConsent(Now);
        }

        db.Users.Add(user);
        var device = UserDevice.Register(user.Id, "phone-" + userName, DevicePlatform.IOS, null, null, null, Now);
        device.UpdatePushToken("ExponentPushToken[" + userName + "]");
        db.UserDevices.Add(device);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<Catalog> CatalogAsync(IdentityTestDb db)
    {
        var country = Country.Create("CO", "Colombia", "Departamento", "es-CO", "COP", "America/Bogota", DistanceUnit.Kilometers, 14, true);
        var region = Region.Create(country.Id, "Cundinamarca", "25");
        var city = City.Create(region.Id, country.Id, "Bogotá", new GeoPoint(4.711, -74.072), "America/Bogota");
        var other = City.Create(region.Id, country.Id, "Medellín", new GeoPoint(6.244, -75.581), "America/Bogota");
        db.AddRange(country, region, city, other);
        await db.SaveChangesAsync();
        return new Catalog(country.Id, region.Id, city.Id, other.Id);
    }

    private sealed record Catalog(Guid CountryId, Guid RegionId, Guid CityId, Guid OtherCityId);

    private sealed class MemoryProximity(IdentityTestDb db) : IAlertLocationQuery
    {
        public async Task<IReadOnlyList<ProximityMatch>> MatchAsync(GeoPoint origin, DateTimeOffset now, CancellationToken cancellationToken)
        {
            var rows = await db.DeviceAlertLocations.Where(item => item.ExpiresAt > now).ToListAsync(cancellationToken);
            var settings = await db.NotificationSettings.ToListAsync(cancellationToken);
            var matches = new List<ProximityMatch>();
            foreach (var group in rows.GroupBy(item => item.UserId))
            {
                var preference = settings.FirstOrDefault(item => item.UserId == group.Key);
                if (preference is { NotifyNearbyReports: false })
                {
                    continue;
                }

                var radius = preference?.NearbyRadiusMeters ?? NotificationSettings.DefaultRadiusMeters;
                var nearest = group.Select(item => origin.DistanceInMetersTo(item.Location)).Min();
                if (nearest <= radius)
                {
                    matches.Add(new ProximityMatch(group.Key, nearest));
                }
            }

            return matches;
        }

        public async Task<IReadOnlyList<ProximityMatch>> MatchWithinAsync(GeoPoint origin, int radiusMeters, DateTimeOffset now, CancellationToken cancellationToken)
        {
            var rows = await db.DeviceAlertLocations.Where(item => item.ExpiresAt > now).ToListAsync(cancellationToken);
            var matches = new List<ProximityMatch>();
            foreach (var group in rows.GroupBy(item => item.UserId))
            {
                var nearest = group.Select(item => origin.DistanceInMetersTo(item.Location)).Min();
                if (nearest <= radiusMeters)
                {
                    matches.Add(new ProximityMatch(group.Key, nearest));
                }
            }

            return matches;
        }
    }

    private sealed class RecordingPush : IPushSender
    {
        public List<PushNote> Notes { get; } = [];
        public bool Reject { get; set; }

        public Task<IReadOnlyList<PushResult>> SendAsync(IReadOnlyList<PushNote> notes, CancellationToken cancellationToken)
        {
            Notes.AddRange(notes);
            IReadOnlyList<PushResult> results = notes
                .Select(note => new PushResult(note.DeviceId, !Reject, Reject, Reject ? "DeviceNotRegistered" : null))
                .ToList();
            return Task.FromResult(results);
        }
    }

    private sealed class Signer : IMediaUrlSigner
    {
        public (long ExpiresUnix, string Signature) Sign(Guid mediaId, DateTimeOffset expiresAt) => (expiresAt.ToUnixTimeSeconds(), "sig");

        public bool IsValid(Guid mediaId, long expiresUnix, string signature, DateTimeOffset now) => signature == "sig";
    }

    private sealed class EchoTokens : ITokenService
    {
        public string CreateAccessToken(Guid userId, DateTimeOffset expiresAt, string? scope = null, string? role = null, Guid? deviceId = null) => "access";
        public string CreateRefreshToken() => "refresh";
        public string Hash(string plainToken) => plainToken;
    }
}
