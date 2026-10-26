using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.MapRealtime.GetMapRealtimeConfig;
using Situ.Application.Features.Reports;
using Situ.Domain.Administration;
using Situ.Domain.Reports;

namespace Situ.Application.Features.MapRealtime.PublishMapZoneNotices;

public sealed record PublishMapZoneNoticesRequest;

public sealed record PublishMapZoneNoticesResponse(int Published, int Cells, int Failed, int CoalesceIntervalMilliseconds);

public sealed class PublishMapZoneNoticesHandler(IApplicationDbContext db, IClock clock, IMapZonePublisher publisher)
    : IHandler<PublishMapZoneNoticesRequest, Result<PublishMapZoneNoticesResponse>>
{
    public async Task<Result<PublishMapZoneNoticesResponse>> Handle(
        PublishMapZoneNoticesRequest request,
        CancellationToken cancellationToken)
    {
        var settings = await ReportSettings.LoadAsync(db, cancellationToken);
        var interval = MapRealtimeLimits.CoalesceMilliseconds(
            settings.GetValueOrDefault(SystemSettingKeys.MapCoalesceIntervalMilliseconds));
        var pending = await db.MapZoneNotices
            .Where(notice => notice.Status == MapZoneNoticeStatus.Pending)
            .OrderBy(notice => notice.OccurredAt)
            .Take(500)
            .ToListAsync(cancellationToken);
        if (pending.Count == 0)
        {
            return Result<PublishMapZoneNoticesResponse>.Success(new PublishMapZoneNoticesResponse(0, 0, 0, interval));
        }

        var published = 0;
        var cells = 0;
        var failed = 0;
        var now = clock.UtcNow;
        foreach (var batch in MapZoneNotice.Coalesce(pending))
        {
            var signals = batch.Notices
                .Select(notice => new MapZoneSignal(notice.Type, notice.ReportId, notice.CellId, notice.OccurredAt))
                .ToArray();
            try
            {
                await publisher.PublishAsync(batch.CellId, signals, cancellationToken);
                foreach (var notice in batch.Notices)
                {
                    notice.MarkPublished(now);
                }

                published += signals.Length;
                cells++;
            }
            catch (Exception)
            {
                failed++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result<PublishMapZoneNoticesResponse>.Success(
            new PublishMapZoneNoticesResponse(published, cells, failed, interval));
    }
}
