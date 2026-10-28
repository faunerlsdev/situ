using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Situ.Application.Common.Abstractions;
using Situ.Application.Features.Live;
using Situ.Domain.Administration;
using Situ.Domain.Common;
using Situ.Domain.Live;
using Situ.Domain.Notifications;
using Situ.Domain.Reports;

namespace Situ.Infrastructure.Persistence.Interceptors;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken);
}

/// <summary>In-process dispatch hook. Handlers are added when the application starts publishing events.</summary>
public sealed class DomainEventDispatcher(ILogger<DomainEventDispatcher> logger, IServiceScopeFactory scopes) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken)
    {
        foreach (var domainEvent in domainEvents)
        {
            if (domainEvent is ReportCreatedEvent created)
            {
                logger.LogInformation(
                    "Report created {ReportId} in category {CategoryId}. Anonymous: {IsAnonymous}.",
                    created.ReportId,
                    created.CategoryId,
                    created.IsAnonymous);
            }

            if (domainEvent is CommentAddedEvent comment)
            {
                logger.LogInformation(
                    "Comment {CommentId} added on report {ReportId}. Anonymous: {IsAnonymous}. The report author is notified later without revealing who commented.",
                    comment.CommentId,
                    comment.ReportId,
                    comment.IsAnonymous);
            }

            if (domainEvent is Situ.Domain.Moderation.ComplaintResolvedEvent resolved)
            {
                logger.LogInformation(
                    "Complaint {ComplaintId} resolved as {Status}. A generic notice is due to the reporter.",
                    resolved.ComplaintId,
                    resolved.Status);
            }
        }

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var now = clock.UtcNow;
            foreach (var domainEvent in domainEvents)
            {
                var (kind, subjectId) = domainEvent switch
                {
                    ReportCreatedEvent created => (NotificationJob.ReportCreated, created.ReportId),
                    CommentAddedEvent comment => (NotificationJob.CommentAdded, comment.CommentId),
                    LiveStreamPublishedEvent published => (NotificationJob.LiveStarted, published.LiveStreamId),
                    LiveStreamEndedEvent ended when ended.WasVisible => (NotificationJob.LiveEnded, ended.LiveStreamId),
                    _ => ((string?)null, Guid.Empty),
                };
                if (kind is null)
                {
                    continue;
                }

                var queued = await db.NotificationJobs.AnyAsync(
                    item => item.Kind == kind && item.SubjectId == subjectId,
                    cancellationToken);
                if (!queued)
                {
                    db.NotificationJobs.Add(NotificationJob.Enqueue(kind, subjectId, now));
                }
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning("Notification outbox insert failed ({ExceptionType}).", exception.GetType().Name);
        }

        try
        {
            await EnqueueMapNoticesAsync(scopes, domainEvents, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning("Map zone outbox insert failed ({ExceptionType}).", exception.GetType().Name);
        }

        try
        {
            await NotifyLiveAsync(scopes, domainEvents, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning("Live map notice failed ({ExceptionType}).", exception.GetType().Name);
        }
    }

    private static async Task NotifyLiveAsync(
        IServiceScopeFactory scopes,
        IReadOnlyCollection<IDomainEvent> domainEvents,
        CancellationToken cancellationToken)
    {
        var signals = new List<(Guid StreamId, string Change)>();
        foreach (var domainEvent in domainEvents)
        {
            switch (domainEvent)
            {
                case LiveStreamPublishedEvent published:
                    signals.Add((published.LiveStreamId, "started"));
                    break;
                case LiveStreamEndedEvent ended when ended.WasVisible:
                    signals.Add((ended.LiveStreamId, "ended"));
                    break;
            }
        }

        if (signals.Count == 0)
        {
            return;
        }

        await using var scope = scopes.CreateAsyncScope();
        var notifier = scope.ServiceProvider.GetService<ILiveNotifier>();
        if (notifier is null)
        {
            return;
        }

        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var now = clock.UtcNow;
        var settings = await db.SystemSettings.Where(item =>
                item.Key == SystemSettingKeys.LiveMinorsCanStream
                || item.Key == SystemSettingKeys.FriendsAllowAdultMinor
                || item.Key == SystemSettingKeys.MapCellDegrees)
            .ToDictionaryAsync(item => item.Key, item => item.Value, cancellationToken);
        var minorsCanStream = string.Equals(settings.GetValueOrDefault(SystemSettingKeys.LiveMinorsCanStream), "true", StringComparison.OrdinalIgnoreCase);
        var allowAdultMinor = string.Equals(settings.GetValueOrDefault(SystemSettingKeys.FriendsAllowAdultMinor), "true", StringComparison.OrdinalIgnoreCase);
        var degrees = MapRealtimeLimits.Degrees(settings.GetValueOrDefault(SystemSettingKeys.MapCellDegrees));
        foreach (var (streamId, change) in signals)
        {
            var stream = await db.LiveStreams.FirstOrDefaultAsync(item => item.Id == streamId, cancellationToken);
            if (stream?.PublicPath is null)
            {
                continue;
            }

            var streamer = await db.Users.FirstOrDefaultAsync(item => item.Id == stream.StreamerId, cancellationToken);
            if (streamer is null)
            {
                continue;
            }

            var friendIds = await db.Friendships
                .Where(item => item.Status == Situ.Domain.Social.FriendshipStatus.Accepted
                    && (item.RequesterId == streamer.Id || item.AddresseeId == streamer.Id))
                .Select(item => item.RequesterId == streamer.Id ? item.AddresseeId : item.RequesterId)
                .ToListAsync(cancellationToken);
            var people = await db.Users.Where(item => friendIds.Contains(item.Id)).ToListAsync(cancellationToken);
            var groups = new List<string>();
            foreach (var friend in people)
            {
                var blocked = await db.UserBlocks.AnyAsync(
                    item => (item.Scope == Situ.Domain.Social.UserBlockScope.Full
                            && ((item.BlockerId == friend.Id && item.BlockedUserId == streamer.Id)
                                || (item.BlockerId == streamer.Id && item.BlockedUserId == friend.Id)))
                        || (item.Scope == Situ.Domain.Social.UserBlockScope.AnonymousContentOnly
                            && item.BlockerId == friend.Id
                            && item.BlockedUserId == streamer.Id),
                    cancellationToken);
                if (LiveAudience.CanWatch(friend, streamer, stream, true, blocked, minorsCanStream, allowAdultMinor, now))
                {
                    groups.Add("user:" + friend.Id.ToString("D"));
                }
            }

            if (stream.Visibility == ContentVisibility.Public && stream.Location is { } location && !streamer.IsMinor(now))
            {
                groups.Add(MapCell.Id(location.Latitude, location.Longitude, degrees));
            }

            if (groups.Count > 0)
            {
                await notifier.ChangedAsync(groups, stream.PublicPath, change, cancellationToken);
            }
        }
    }

    private static async Task EnqueueMapNoticesAsync(
        IServiceScopeFactory scopes,
        IReadOnlyCollection<IDomainEvent> domainEvents,
        CancellationToken cancellationToken)
    {
        var relevant = new List<(Guid ReportId, string Type)>();
        foreach (var domainEvent in domainEvents)
        {
            switch (domainEvent)
            {
                case ReportCreatedEvent created:
                    relevant.Add((created.ReportId, MapZoneNotice.Created));
                    break;
                case ReportMapChanged changed when MapZoneNotice.IsKnown(changed.Kind):
                    relevant.Add((changed.ReportId, changed.Kind));
                    break;
            }
        }

        if (relevant.Count == 0)
        {
            return;
        }

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var rawDegrees = await db.SystemSettings
            .Where(setting => setting.Key == SystemSettingKeys.MapCellDegrees)
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync(cancellationToken);
        var degrees = MapRealtimeLimits.Degrees(rawDegrees);
        var now = clock.UtcNow;
        var reportIds = relevant.Select(item => item.ReportId).Distinct().ToArray();
        var reports = await db.Reports.Where(report => reportIds.Contains(report.Id)).ToDictionaryAsync(report => report.Id, cancellationToken);
        var queued = 0;
        foreach (var (reportId, type) in relevant)
        {
            if (!reports.TryGetValue(reportId, out var report) || !MapZonePolicy.EmitsZoneEvent(report))
            {
                continue;
            }

            var cellId = MapCell.Id(report.Location.Latitude, report.Location.Longitude, degrees);
            db.MapZoneNotices.Add(MapZoneNotice.Enqueue(type, report.Id, cellId, now));
            queued++;
        }

        if (queued > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}

public sealed class DomainEventSaveChangesInterceptor(IDomainEventDispatcher dispatcher) : SaveChangesInterceptor
{
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        DispatchAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await DispatchAsync(eventData.Context, cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private async Task DispatchAsync(DbContext? context, CancellationToken cancellationToken)
    {
        if (context is null)
        {
            return;
        }

        var aggregates = context.ChangeTracker
            .Entries<AggregateRoot>()
            .Select(entry => entry.Entity)
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToList();

        if (aggregates.Count == 0)
        {
            return;
        }

        var domainEvents = aggregates.SelectMany(aggregate => aggregate.DomainEvents).ToArray();
        await dispatcher.DispatchAsync(domainEvents, cancellationToken);

        foreach (var aggregate in aggregates)
        {
            aggregate.ClearDomainEvents();
        }
    }
}
