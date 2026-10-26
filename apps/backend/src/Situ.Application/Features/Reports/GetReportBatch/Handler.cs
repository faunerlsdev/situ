using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Reports.GetReportMap;
using Situ.Domain.Common;
using Situ.Domain.Reports;

namespace Situ.Application.Features.Reports.GetReportBatch;

public sealed record GetReportBatchRequest(Guid ViewerId, IReadOnlyList<Guid> Ids, double? Latitude, double? Longitude);

public sealed record GetReportBatchResponse(IReadOnlyList<MapItemView> Items);

public sealed class GetReportBatchHandler(IApplicationDbContext db, IClock clock, IMediaUrlSigner urls)
    : IHandler<GetReportBatchRequest, Result<GetReportBatchResponse>>
{
    public const int MaxIds = 50;

    public async Task<Result<GetReportBatchResponse>> Handle(GetReportBatchRequest request, CancellationToken cancellationToken)
    {
        var ids = new List<Guid>(request.Ids.Count);
        var seen = new HashSet<Guid>();
        foreach (var id in request.Ids)
        {
            if (seen.Add(id))
            {
                ids.Add(id);
            }
        }

        if (ids.Count > MaxIds)
        {
            return Result<GetReportBatchResponse>.Failure(
                Error.Validation("report.batch_too_large", "Puedes consultar hasta 50 reportes a la vez."));
        }

        if (ids.Count == 0)
        {
            return Result<GetReportBatchResponse>.Success(new GetReportBatchResponse([]));
        }

        var now = clock.UtcNow;
        var viewer = await db.Users.FirstOrDefaultAsync(item => item.Id == request.ViewerId, cancellationToken);
        if (viewer is null)
        {
            return Result<GetReportBatchResponse>.Failure(ReportErrors.NotFound());
        }

        var reports = await db.Reports.Where(item => ids.Contains(item.Id)).ToListAsync(cancellationToken);
        var byId = reports.ToDictionary(item => item.Id);
        var blocks = await ReportAccess.BlocksAsync(db, request.ViewerId, cancellationToken);
        var friends = await ReportAccess.FriendIdsAsync(db, request.ViewerId, cancellationToken);
        var viewerIsMinor = viewer.IsMinor(now);
        var visible = new List<Report>();
        foreach (var id in ids)
        {
            if (!byId.TryGetValue(id, out var report) || !report.IsVisible(now))
            {
                continue;
            }

            if (!ReportAccess.CanSee(report, request.ViewerId, friends, blocks))
            {
                continue;
            }

            visible.Add(report);
        }

        var categoryIds = visible.Select(item => item.CategoryId).Distinct().ToArray();
        var categories = await db.ReportCategories.Where(item => categoryIds.Contains(item.Id)).ToListAsync(cancellationToken);
        var categoryById = categories.ToDictionary(item => item.Id);
        if (viewerIsMinor)
        {
            visible = visible.Where(item => categoryById.TryGetValue(item.CategoryId, out var category) && !category.IsAdultOnly).ToList();
        }

        var settings = await ReportSettings.LoadAsync(db, cancellationToken);
        var anonymousName = ReportSettings.DisplayName(settings);
        var authorIds = visible.Select(item => item.AuthorId).Distinct().ToArray();
        var users = await db.Users.Where(item => authorIds.Contains(item.Id)).ToListAsync(cancellationToken);
        var profiles = await db.UserProfiles.Where(item => authorIds.Contains(item.UserId)).ToListAsync(cancellationToken);
        var reportIds = visible.Select(item => item.Id).ToArray();
        var media = await db.ReportMedias
            .Where(item => item.ReportId != null && reportIds.Contains(item.ReportId.Value) && item.ProcessingStatus == MediaProcessingStatus.Ready)
            .OrderBy(item => item.SortOrder)
            .ToListAsync(cancellationToken);
        var previewByReport = new Dictionary<Guid, ReportMedia>();
        foreach (var item in media)
        {
            if (item.ReportId is { } reportId && !previewByReport.ContainsKey(reportId))
            {
                previewByReport[reportId] = item;
            }
        }

        GeoPoint? viewerPoint = request.Latitude is { } latitude && request.Longitude is { } longitude
            && latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180
            ? new GeoPoint(latitude, longitude)
            : null;
        var items = new List<MapItemView>(visible.Count);
        foreach (var report in visible)
        {
            categoryById.TryGetValue(report.CategoryId, out var category);
            string? preview = null;
            if (previewByReport.TryGetValue(report.Id, out var file))
            {
                var signed = urls.Sign(file.Id, now.AddMinutes(15));
                preview = ReportPresentation.ReadUrl(file.Id, signed.ExpiresUnix, signed.Signature);
            }

            var authorUser = users.FirstOrDefault(item => item.Id == report.AuthorId);
            var author = ReportPresentation.Author(
                report.IsAnonymous,
                authorUser,
                report.IsAnonymous ? null : profiles.FirstOrDefault(item => item.UserId == report.AuthorId),
                anonymousName);
            double? distance = viewerPoint is { } point ? point.DistanceInMetersTo(report.Location) : null;
            items.Add(new MapItemView(
                "point",
                report.Id,
                1,
                report.Location.Latitude,
                report.Location.Longitude,
                report.CategoryId,
                category?.Code,
                report.CreatedAt,
                distance,
                author,
                report.StillThereCount,
                report.GoneNowCount,
                report.CommentCount,
                preview,
                report.IsAnonymous,
                report.HideAuthorOnMap,
                report.AuthorId == request.ViewerId));
        }

        return Result<GetReportBatchResponse>.Success(new GetReportBatchResponse(items));
    }
}
