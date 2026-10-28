using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Live;
using Situ.Application.Features.Reports;
using Situ.Domain.Administration;
using Situ.Domain.Common;
using Situ.Domain.Guardianship;
using Situ.Domain.Identity;
using Situ.Domain.Live;
using Situ.Domain.Notifications;
using Situ.Domain.Reports;
using Situ.Domain.Social;

namespace Situ.Application.Features.Notifications.Dispatch;

public sealed record ProcessNotificationJobsRequest;

public sealed record ProcessNotificationJobsResponse(int Processed, int Retried);

public sealed class ProcessNotificationJobsHandler(
    IApplicationDbContext db,
    IClock clock,
    IAlertLocationQuery proximity,
    IPushSender push)
    : IHandler<ProcessNotificationJobsRequest, Result<ProcessNotificationJobsResponse>>
{
    public async Task<Result<ProcessNotificationJobsResponse>> Handle(ProcessNotificationJobsRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var settings = await ReportSettings.LoadAsync(db, cancellationToken);
        var batch = Math.Clamp(ReportSettings.Int(settings, SystemSettingKeys.NotificationDispatchBatchSize, 50), 1, 200);
        var pushLimit = Math.Clamp(ReportSettings.Int(settings, SystemSettingKeys.NotificationMaxPushesPerHour, 6), 1, 100);
        var pushBatch = Math.Clamp(ReportSettings.Int(settings, SystemSettingKeys.NotificationPushBatchSize, 100), 1, ExpoBatchLimit);
        var jobs = await db.NotificationJobs
            .Where(item => item.Status == NotificationJobStatus.Pending && item.AvailableAt <= now)
            .OrderBy(item => item.AvailableAt)
            .Take(batch)
            .ToListAsync(cancellationToken);
        var processed = 0;
        var retried = 0;
        foreach (var job in jobs)
        {
            try
            {
                if (job.Kind == NotificationJob.ReportCreated)
                {
                    await FanOutReportAsync(job.SubjectId, now, pushLimit, pushBatch, cancellationToken);
                }
                else if (job.Kind == NotificationJob.CommentAdded)
                {
                    await FanOutCommentAsync(job.SubjectId, now, pushLimit, pushBatch, cancellationToken);
                }
                else if (job.Kind is NotificationJob.LiveStarted or NotificationJob.LiveEnded)
                {
                    await FanOutLiveAsync(job.SubjectId, job.Kind == NotificationJob.LiveStarted, now, pushLimit, pushBatch, cancellationToken);
                }

                job.Complete(now);
                await db.SaveChangesAsync(cancellationToken);
                processed++;
            }
            catch (DbUpdateException)
            {
                job.Retry(now, "dispatch_conflict");
                await db.SaveChangesAsync(cancellationToken);
                retried++;
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                job.Retry(now, "dispatch_failed");
                await db.SaveChangesAsync(cancellationToken);
                retried++;
            }
        }

        return Result<ProcessNotificationJobsResponse>.Success(new ProcessNotificationJobsResponse(processed, retried));
    }

    private const int ExpoBatchLimit = 100;

    private async Task FanOutLiveAsync(Guid streamId, bool started, DateTimeOffset now, int pushLimit, int pushBatch, CancellationToken cancellationToken)
    {
        var stream = await db.LiveStreams.FirstOrDefaultAsync(item => item.Id == streamId, cancellationToken);
        if (stream is null || string.IsNullOrWhiteSpace(stream.PublicPath))
        {
            return;
        }

        if (started && stream.Status != LiveStreamStatus.Live)
        {
            return;
        }

        if (!started && stream.Status is not (LiveStreamStatus.Ended or LiveStreamStatus.Cut))
        {
            return;
        }

        var streamer = await db.Users.FirstOrDefaultAsync(item => item.Id == stream.StreamerId, cancellationToken);
        if (streamer is null)
        {
            return;
        }

        var settings = await ReportSettings.LoadAsync(db, cancellationToken);
        var minorsCanStream = string.Equals(settings.GetValueOrDefault(SystemSettingKeys.LiveMinorsCanStream), "true", StringComparison.OrdinalIgnoreCase);
        var allowAdultMinor = string.Equals(settings.GetValueOrDefault(SystemSettingKeys.FriendsAllowAdultMinor), "true", StringComparison.OrdinalIgnoreCase);
        var radius = Math.Clamp(ReportSettings.Int(settings, SystemSettingKeys.LiveNotifyRadiusMeters, 1000), 100, 10_000);
        var audience = new Dictionary<Guid, bool>();
        if (stream.Visibility == ContentVisibility.FriendsOnly)
        {
            var friendIds = await db.Friendships
                .Where(item => item.Status == FriendshipStatus.Accepted && (item.RequesterId == streamer.Id || item.AddresseeId == streamer.Id))
                .Select(item => item.RequesterId == streamer.Id ? item.AddresseeId : item.RequesterId)
                .ToListAsync(cancellationToken);
            foreach (var friendId in friendIds.Where(item => item != streamer.Id))
            {
                audience[friendId] = true;
            }
        }
        else if (started && stream.Location is { } location)
        {
            var nearby = await proximity.MatchWithinAsync(location, radius, now, cancellationToken);
            foreach (var match in nearby.Where(item => item.UserId != streamer.Id))
            {
                audience[match.UserId] = false;
            }

            if (stream.CityId is { } cityId)
            {
                var interested = await db.UserInterestCities
                    .Where(item => item.CityId == cityId && item.UserId != streamer.Id)
                    .Select(item => item.UserId)
                    .ToListAsync(cancellationToken);
                foreach (var userId in interested)
                {
                    audience.TryAdd(userId, false);
                }
            }
        }

        if (audience.Count == 0)
        {
            return;
        }

        var ids = audience.Keys.ToArray();
        var users = await db.Users.Where(item => ids.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        var preferences = await db.NotificationSettings.Where(item => ids.Contains(item.UserId)).ToDictionaryAsync(item => item.UserId, cancellationToken);
        var devices = await db.UserDevices.Where(item => ids.Contains(item.UserId) && item.RevokedAt == null && item.PushToken != null).ToListAsync(cancellationToken);
        var pushes = new List<(NotificationDelivery Delivery, PushNote Note)>();
        foreach (var (userId, friendCopy) in audience)
        {
            if (!users.TryGetValue(userId, out var viewer) || IsClosed(viewer.Status))
            {
                continue;
            }

            preferences.TryGetValue(viewer.Id, out var preference);
            if (friendCopy && preference is { NotifyFriendLives: false })
            {
                continue;
            }

            if (!friendCopy && preference is { NotifyNearbyLives: false })
            {
                continue;
            }

            var type = friendCopy
                ? started ? NotificationType.FriendLiveStarted : NotificationType.FriendLiveEnded
                : NotificationType.NearbyLiveStarted;
            var muted = await db.NotificationPreferences.AnyAsync(
                item => item.UserId == viewer.Id && item.Type == type && item.Channel == NotificationChannel.InApp && !item.IsEnabled,
                cancellationToken);
            if (muted)
            {
                continue;
            }

            var blocked = await db.UserBlocks.AnyAsync(
                item => (item.Scope == UserBlockScope.Full
                        && ((item.BlockerId == viewer.Id && item.BlockedUserId == streamer.Id) || (item.BlockerId == streamer.Id && item.BlockedUserId == viewer.Id)))
                    || (item.Scope == UserBlockScope.AnonymousContentOnly && item.BlockerId == viewer.Id && item.BlockedUserId == streamer.Id),
                cancellationToken);
            if (!LiveAudience.CanWatch(viewer, streamer, stream, friendCopy, blocked, minorsCanStream, allowAdultMinor, now))
            {
                continue;
            }

            var already = await db.Notifications.AnyAsync(
                item => item.RecipientId == viewer.Id && item.Type == type && item.TargetType == ContentTargetType.LiveStream && item.TargetId == stream.Id,
                cancellationToken);
            if (already)
            {
                continue;
            }

            var body = friendCopy
                ? started ? "Un amigo está en vivo." : "La transmisión de un amigo terminó."
                : "Hay una transmisión en vivo cerca de ti.";
            var notification = Notification.Create(viewer.Id, type, "En vivo", body, ContentTargetType.LiveStream, stream.Id, now);
            db.Notifications.Add(notification);
            await QueuePushAsync(viewer, preference, notification, null, devices, pushes, now, pushLimit, cancellationToken, stream.PublicPath);
        }

        await FinishPushAsync(pushes, devices, pushBatch, cancellationToken);
    }

    private async Task FanOutReportAsync(Guid reportId, DateTimeOffset now, int pushLimit, int pushBatch, CancellationToken cancellationToken)
    {
        var report = await db.Reports.FirstOrDefaultAsync(item => item.Id == reportId, cancellationToken);
        if (report is null || !report.IsVisible(now) || report.Status != ReportStatus.Active)
        {
            return;
        }

        var category = await db.ReportCategories.FirstOrDefaultAsync(item => item.Id == report.CategoryId, cancellationToken);
        var city = report.CityId is { } cityId
            ? await db.Cities.FirstOrDefaultAsync(item => item.Id == cityId, cancellationToken)
            : null;
        var categoryName = category?.Name ?? "Reporte";
        var cityName = city?.Name ?? "tu ciudad";
        var triggers = new Dictionary<Guid, Candidate>();

        if (report.Visibility == ContentVisibility.Public)
        {
            if (report.CityId is { } reportCity)
            {
                var home = await db.Users.Where(item => item.CityId == reportCity && item.Id != report.AuthorId).Select(item => item.Id).ToListAsync(cancellationToken);
                var extra = await db.UserInterestCities.Where(item => item.CityId == reportCity && item.UserId != report.AuthorId).Select(item => item.UserId).ToListAsync(cancellationToken);
                foreach (var userId in home.Concat(extra))
                {
                    Slot(triggers, userId).City = true;
                }
            }

            var nearby = await proximity.MatchAsync(report.Location, now, cancellationToken);
            foreach (var match in nearby.Where(item => item.UserId != report.AuthorId))
            {
                var slot = Slot(triggers, match.UserId);
                if (slot.DistanceMeters is null || match.DistanceMeters < slot.DistanceMeters)
                {
                    slot.DistanceMeters = match.DistanceMeters;
                }
            }
        }

        if (!report.IsAnonymous)
        {
            var friends = await db.Friendships
                .Where(item => item.Status == FriendshipStatus.Accepted && (item.RequesterId == report.AuthorId || item.AddresseeId == report.AuthorId))
                .ToListAsync(cancellationToken);
            foreach (var friendship in friends)
            {
                var friendId = friendship.RequesterId == report.AuthorId ? friendship.AddresseeId : friendship.RequesterId;
                if (friendId != report.AuthorId)
                {
                    Slot(triggers, friendId).Friend = true;
                }
            }
        }

        await DeliverAsync(triggers.Values, report, category, categoryName, cityName, now, pushLimit, pushBatch, cancellationToken);
    }

    private async Task DeliverAsync(
        IEnumerable<Candidate> candidates,
        Report report,
        ReportCategory? category,
        string categoryName,
        string cityName,
        DateTimeOffset now,
        int pushLimit,
        int pushBatch,
        CancellationToken cancellationToken)
    {
        var list = candidates.ToList();
        if (list.Count == 0)
        {
            return;
        }

        var ids = list.Select(item => item.UserId).ToArray();
        var users = await db.Users.Where(item => ids.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        var preferences = await db.NotificationSettings.Where(item => ids.Contains(item.UserId)).ToDictionaryAsync(item => item.UserId, cancellationToken);
        var muted = await db.UserCategoryInterests
            .Where(item => ids.Contains(item.UserId) && item.CategoryId == report.CategoryId && item.IsMuted)
            .Select(item => item.UserId)
            .ToListAsync(cancellationToken);
        var blocks = await db.UserBlocks
            .Where(item => item.BlockerId == report.AuthorId || item.BlockedUserId == report.AuthorId || ids.Contains(item.BlockerId) || ids.Contains(item.BlockedUserId))
            .ToListAsync(cancellationToken);
        var overrides = await db.ReportCategoryOverrides
            .Where(item => item.CategoryId == report.CategoryId && !item.IsEnabled && item.CityId != null)
            .Select(item => item.CityId!.Value)
            .ToListAsync(cancellationToken);
        var devices = await db.UserDevices
            .Where(item => ids.Contains(item.UserId) && item.RevokedAt == null && item.PushToken != null)
            .ToListAsync(cancellationToken);
        var pushes = new List<(NotificationDelivery Delivery, PushNote Note)>();

        foreach (var candidate in list)
        {
            if (!users.TryGetValue(candidate.UserId, out var user) || !Eligible(user, report, category, muted, blocks, overrides, now))
            {
                continue;
            }

            preferences.TryGetValue(user.Id, out var preference);
            var choice = Choose(candidate, preference);
            if (choice is null)
            {
                continue;
            }

            var already = await db.Notifications.AnyAsync(
                item => item.RecipientId == user.Id && item.TargetType == ContentTargetType.Report && item.TargetId == report.Id,
                cancellationToken);
            if (already)
            {
                continue;
            }

            var copy = choice.Value switch
            {
                NotificationType.FriendReport => NotificationCopy.Friend(categoryName),
                NotificationType.NearbyReport when candidate.DistanceMeters is { } distance => NotificationCopy.Nearby(categoryName, distance),
                _ => NotificationCopy.City(cityName, categoryName),
            };
            var notification = Notification.Create(user.Id, choice.Value, copy.Title, copy.Body, ContentTargetType.Report, report.Id, now);
            db.Notifications.Add(notification);
            await QueuePushAsync(user, preference, notification, report.Id, devices, pushes, now, pushLimit, cancellationToken);
        }

        await FinishPushAsync(pushes, devices, pushBatch, cancellationToken);
    }

    private async Task FanOutCommentAsync(Guid commentId, DateTimeOffset now, int pushLimit, int pushBatch, CancellationToken cancellationToken)
    {
        var comment = await db.Comments.FirstOrDefaultAsync(item => item.Id == commentId && item.Status == CommentStatus.Visible, cancellationToken);
        if (comment is null)
        {
            return;
        }

        var report = await db.Reports.FirstOrDefaultAsync(item => item.Id == comment.TargetId, cancellationToken);
        if (report is null || report.AuthorId == comment.AuthorId)
        {
            return;
        }

        var blocks = await db.UserBlocks
            .Where(item => (item.BlockerId == report.AuthorId && item.BlockedUserId == comment.AuthorId) || (item.BlockerId == comment.AuthorId && item.BlockedUserId == report.AuthorId))
            .ToListAsync(cancellationToken);
        if (blocks.Any(item => item.Scope == UserBlockScope.Full)
            || (comment.IsAnonymous && blocks.Any(item => item.Scope == UserBlockScope.AnonymousContentOnly && item.BlockerId == report.AuthorId)))
        {
            return;
        }

        var author = await db.Users.FirstOrDefaultAsync(item => item.Id == report.AuthorId, cancellationToken);
        if (author is null || IsClosed(author.Status))
        {
            return;
        }

        var exists = await db.Notifications.AnyAsync(
            item => item.RecipientId == author.Id && item.TargetType == ContentTargetType.Comment && item.TargetId == comment.Id,
            cancellationToken);
        if (exists)
        {
            return;
        }

        var copy = NotificationCopy.Comment();
        var notification = Notification.Create(author.Id, NotificationType.Comment, copy.Title, copy.Body, ContentTargetType.Comment, comment.Id, now);
        db.Notifications.Add(notification);
        var preference = await db.NotificationSettings.FirstOrDefaultAsync(item => item.UserId == author.Id, cancellationToken);
        var devices = await db.UserDevices.Where(item => item.UserId == author.Id && item.RevokedAt == null && item.PushToken != null).ToListAsync(cancellationToken);
        var pushes = new List<(NotificationDelivery Delivery, PushNote Note)>();
        await QueuePushAsync(author, preference, notification, report.Id, devices, pushes, now, pushLimit, cancellationToken);
        await FinishPushAsync(pushes, devices, pushBatch, cancellationToken);
    }

    private async Task QueuePushAsync(
        User user,
        NotificationSettings? preference,
        Notification notification,
        Guid? reportId,
        List<UserDevice> devices,
        List<(NotificationDelivery Delivery, PushNote Note)> pushes,
        DateTimeOffset now,
        int pushLimit,
        CancellationToken cancellationToken,
        string? livePublicId = null)
    {
        var pushOn = preference?.PushEnabled ?? true;
        if (!pushOn)
        {
            db.NotificationDeliveries.Add(NotificationDelivery.Create(notification.Id, NotificationChannel.Push, null, now).Also(item => item.Suppress("push_disabled")));
            return;
        }

        if (preference is not null && Quiet(preference, now))
        {
            db.NotificationDeliveries.Add(NotificationDelivery.Create(notification.Id, NotificationChannel.Push, null, now).Also(item => item.Suppress("quiet_hours")));
            return;
        }

        var since = now.AddHours(-1);
        var sent = await (
            from delivery in db.NotificationDeliveries
            join item in db.Notifications on delivery.NotificationId equals item.Id
            where item.RecipientId == user.Id && delivery.Channel == NotificationChannel.Push && delivery.Status == DeliveryStatus.Sent && delivery.SentAt >= since
            select delivery).CountAsync(cancellationToken);
        if (sent >= pushLimit)
        {
            db.NotificationDeliveries.Add(NotificationDelivery.Create(notification.Id, NotificationChannel.Push, null, now).Also(item => item.Suppress("hourly_limit")));
            await EnsureDigestAsync(user, preference, devices, pushes, now, cancellationToken);
            return;
        }

        var badge = await UnreadBadgeAsync(user.Id, cancellationToken);
        var payload = PushPayload.For(notification.Id, notification.Type.ToString(), notification.TargetType?.ToString(), notification.TargetId, reportId);
        if (!string.IsNullOrWhiteSpace(livePublicId))
        {
            payload = payload with { TargetId = livePublicId };
        }
        foreach (var device in devices.Where(item => item.UserId == user.Id && !string.IsNullOrWhiteSpace(item.PushToken)))
        {
            var delivery = NotificationDelivery.Create(notification.Id, NotificationChannel.Push, device.Id, now);
            db.NotificationDeliveries.Add(delivery);
            pushes.Add((delivery, new PushNote(device.Id, device.PushToken!, notification.Title, notification.Body, payload, badge)));
        }
    }

    private async Task EnsureDigestAsync(
        User user,
        NotificationSettings? preference,
        List<UserDevice> devices,
        List<(NotificationDelivery Delivery, PushNote Note)> pushes,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var local = TimeZoneInfo.ConvertTime(now, TimeZone(preference));
        var key = $"zone:{local:yyyyMMddHH}";
        var existing = await db.Notifications.FirstOrDefaultAsync(item => item.RecipientId == user.Id && item.GroupKey == key, cancellationToken);
        var copy = NotificationCopy.Digest((existing?.BundleCount ?? 0) + 1);
        if (existing is null)
        {
            var created = Notification.Digest(user.Id, key, copy.Title, copy.Body, 1, now);
            db.Notifications.Add(created);
            var badge = await UnreadBadgeAsync(user.Id, cancellationToken);
            var payload = PushPayload.Summary(created.Id);
            foreach (var device in devices.Where(item => item.UserId == user.Id && !string.IsNullOrWhiteSpace(item.PushToken)))
            {
                var delivery = NotificationDelivery.Create(created.Id, NotificationChannel.Push, device.Id, now);
                db.NotificationDeliveries.Add(delivery);
                pushes.Add((delivery, new PushNote(device.Id, device.PushToken!, created.Title, created.Body, payload, badge)));
            }

            return;
        }

        existing.Rewrite(copy.Title, copy.Body, existing.BundleCount + 1, now);
    }

    private async Task FinishPushAsync(
        List<(NotificationDelivery Delivery, PushNote Note)> pushes,
        List<UserDevice> devices,
        int pushBatch,
        CancellationToken cancellationToken)
    {
        if (pushes.Count == 0)
        {
            return;
        }

        var results = new List<PushResult>(pushes.Count);
        for (var offset = 0; offset < pushes.Count; offset += pushBatch)
        {
            var slice = pushes.Skip(offset).Take(pushBatch).Select(item => item.Note).ToList();
            results.AddRange(await push.SendAsync(slice, cancellationToken));
        }

        var now = clock.UtcNow;
        for (var index = 0; index < pushes.Count; index++)
        {
            var result = index < results.Count ? results[index] : new PushResult(pushes[index].Note.DeviceId, false, false, "push_failed");
            if (result.DeviceNotRegistered)
            {
                devices.FirstOrDefault(item => item.Id == result.DeviceId)?.UpdatePushToken(null);
                pushes[index].Delivery.MarkFailed("DeviceNotRegistered");
            }
            else if (result.Accepted)
            {
                pushes[index].Delivery.MarkSent(now);
            }
            else
            {
                pushes[index].Delivery.MarkFailed(result.ErrorCode ?? "push_failed");
            }
        }
    }

    private async Task<int> UnreadBadgeAsync(Guid userId, CancellationToken cancellationToken)
    {
        var stored = await db.Notifications.AsNoTracking()
            .Where(item => item.RecipientId == userId && item.ReadAt == null)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        var unsaved = db.Notifications.Local.Count(item => item.RecipientId == userId && item.ReadAt == null && !stored.Contains(item.Id));
        return stored.Count + unsaved;
    }

    private static Candidate Slot(Dictionary<Guid, Candidate> triggers, Guid userId)
    {
        if (!triggers.TryGetValue(userId, out var candidate))
        {
            candidate = new Candidate(userId);
            triggers[userId] = candidate;
        }

        return candidate;
    }

    private static bool Eligible(User user, Report report, ReportCategory? category, List<Guid> muted, List<UserBlock> blocks, List<Guid> disabledCities, DateTimeOffset now)
    {
        if (user.Id == report.AuthorId || IsClosed(user.Status) || muted.Contains(user.Id))
        {
            return false;
        }

        if (category is { IsAdultOnly: true } && !MinorProtectionPolicy.CanUseAdultOnlyCategories(user.AgeOn(now)))
        {
            return false;
        }

        return !disabledCities.Contains(user.CityId) && !blocks.Any(item => Blocks(item, report.AuthorId, user.Id, report.IsAnonymous));
    }

    private static bool IsClosed(UserStatus status) =>
        status is UserStatus.Suspended or UserStatus.Banned or UserStatus.Deleted or UserStatus.DeletionRequested;

    private static bool Blocks(UserBlock block, Guid authorId, Guid recipientId, bool anonymous) =>
        (block.Scope == UserBlockScope.Full
            && ((block.BlockerId == authorId && block.BlockedUserId == recipientId) || (block.BlockerId == recipientId && block.BlockedUserId == authorId)))
        || (anonymous && block.Scope == UserBlockScope.AnonymousContentOnly && block.BlockerId == recipientId && block.BlockedUserId == authorId);

    private static NotificationType? Choose(Candidate candidate, NotificationSettings? preference)
    {
        var nearby = preference?.NotifyNearbyReports ?? true;
        var friends = preference?.NotifyFriendReports ?? true;
        if (candidate.Friend && friends)
        {
            return NotificationType.FriendReport;
        }

        if (candidate.DistanceMeters is not null && nearby)
        {
            return NotificationType.NearbyReport;
        }

        if (candidate.City && nearby)
        {
            return NotificationType.NearbyReport;
        }

        return null;
    }

    private static bool Quiet(NotificationSettings preference, DateTimeOffset now)
    {
        var local = TimeZoneInfo.ConvertTime(now, TimeZone(preference));
        return preference.IsQuietAt(TimeOnly.FromDateTime(local.DateTime));
    }

    private static TimeZoneInfo TimeZone(NotificationSettings? preference)
    {
        var id = preference?.TimeZoneId ?? NotificationSettings.DefaultTimeZoneId;
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(NotificationSettings.DefaultTimeZoneId);
        }
    }

    private sealed class Candidate(Guid userId)
    {
        public Guid UserId { get; } = userId;
        public bool City { get; set; }
        public bool Friend { get; set; }
        public double? DistanceMeters { get; set; }
    }
}

internal static class DeliveryExtensions
{
    public static NotificationDelivery Also(this NotificationDelivery delivery, Action<NotificationDelivery> apply)
    {
        apply(delivery);
        return delivery;
    }
}
