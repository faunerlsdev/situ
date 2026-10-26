using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Situ.Application.Common.Abstractions;
using Situ.Domain.Administration;
using Situ.Domain.Common;
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
