using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Domain.Administration;
using Situ.Domain.Common;
using Situ.Domain.Reports;
using Situ.Domain.Retention;

namespace Situ.Application.Features.Reports.ExpireReports
{

public sealed record ExpireReportsRequest;

public sealed record ExpireReportsResponse(int Expired);

public sealed class ExpireReportsHandler(IApplicationDbContext db, IClock clock)
    : IHandler<ExpireReportsRequest, Result<ExpireReportsResponse>>
{
    public async Task<Result<ExpireReportsResponse>> Handle(ExpireReportsRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var due = await db.Reports
            .Where(report => report.Status == ReportStatus.Active && report.ExpiresAt <= now)
            .ToListAsync(cancellationToken);
        foreach (var report in due)
        {
            report.Expire(now);
        }

        if (due.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return Result<ExpireReportsResponse>.Success(new ExpireReportsResponse(due.Count));
    }
}

}

namespace Situ.Application.Features.Reports.PurgePendingReportMedia
{

public sealed record PurgePendingReportMediaRequest;

public sealed record PurgePendingReportMediaResponse(int Deleted);

public sealed class PurgePendingReportMediaHandler(IApplicationDbContext db, IClock clock, IFileStorage storage)
    : IHandler<PurgePendingReportMediaRequest, Result<PurgePendingReportMediaResponse>>
{
    public async Task<Result<PurgePendingReportMediaResponse>> Handle(
        PurgePendingReportMediaRequest request,
        CancellationToken cancellationToken)
    {
        var settings = await ReportSettings.LoadAsync(db, cancellationToken);
        var hours = ReportSettings.Int(settings, SystemSettingKeys.ReportPendingMediaHours, 24);
        var cutoff = clock.UtcNow.AddHours(-hours);
        var stale = await db.ReportMedias
            .Where(media => media.ReportId == null && media.CreatedAt <= cutoff)
            .ToListAsync(cancellationToken);
        foreach (var media in stale)
        {
            await storage.DeleteAsync(media.StorageKey, cancellationToken);
            if (media.ThumbnailKey is { } thumbnail)
            {
                await storage.DeleteAsync(thumbnail, cancellationToken);
            }

            db.ReportMedias.Remove(media);
        }

        if (stale.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return Result<PurgePendingReportMediaResponse>.Success(new PurgePendingReportMediaResponse(stale.Count));
    }
}

}

namespace Situ.Application.Features.Reports.RetainReports
{

public sealed record RetainReportsRequest;

public sealed record RetainReportsResponse(int Purged);

public sealed class RetainReportsHandler(IApplicationDbContext db, IClock clock, IFileStorage storage)
    : IHandler<RetainReportsRequest, Result<RetainReportsResponse>>
{
    public async Task<Result<RetainReportsResponse>> Handle(RetainReportsRequest request, CancellationToken cancellationToken)
    {
        var settings = await ReportSettings.LoadAsync(db, cancellationToken);
        var days = ReportSettings.Int(settings, SystemSettingKeys.ReportRetentionDays, 365);
        var policy = await db.RetentionPolicies.FirstOrDefaultAsync(
            item => item.Kind == RetentionDataKind.Report && item.IsActive,
            cancellationToken);
        if (policy is not null)
        {
            days = policy.RetainDays;
        }

        var cutoff = clock.UtcNow.AddDays(-days);
        var held = await db.LegalHolds
            .Where(hold => hold.TargetType == ContentTargetType.Report && hold.ReleasedAt == null)
            .Select(hold => hold.TargetId)
            .ToListAsync(cancellationToken);
        var due = await db.Reports
            .Where(report => (report.Status == ReportStatus.Expired || report.Status == ReportStatus.Removed)
                && (report.UpdatedAt ?? report.ExpiresAt) <= cutoff
                && !held.Contains(report.Id))
            .ToListAsync(cancellationToken);
        var purged = 0;
        foreach (var report in due)
        {
            var media = await db.ReportMedias.Where(item => item.ReportId == report.Id).ToListAsync(cancellationToken);
            foreach (var file in media)
            {
                await storage.DeleteAsync(file.StorageKey, cancellationToken);
                if (file.ThumbnailKey is { } thumbnail)
                {
                    await storage.DeleteAsync(thumbnail, cancellationToken);
                }

                db.ReportMedias.Remove(file);
            }

            var confirmations = await db.ReportConfirmations.Where(item => item.ReportId == report.Id).ToListAsync(cancellationToken);
            db.ReportConfirmations.RemoveRange(confirmations);
            var comments = await db.Comments.Where(item => item.TargetType == ContentTargetType.Report && item.TargetId == report.Id).ToListAsync(cancellationToken);
            db.Comments.RemoveRange(comments);
            db.Reports.Remove(report);
            purged++;
        }

        if (purged > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return Result<RetainReportsResponse>.Success(new RetainReportsResponse(purged));
    }
}
}
