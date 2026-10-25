using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Domain.Guardianship;
using Situ.Domain.Reports;

namespace Situ.Application.Features.Reports.OpenReportMedia;

public sealed record OpenReportMediaRequest(Guid? ViewerId, Guid MediaId, bool Signed, bool Thumbnail);

public sealed record OpenReportMediaResponse(string StorageKey, string ContentType);

public sealed class OpenReportMediaHandler(IApplicationDbContext db, IClock clock)
    : IHandler<OpenReportMediaRequest, Result<OpenReportMediaResponse>>
{
    public async Task<Result<OpenReportMediaResponse>> Handle(OpenReportMediaRequest request, CancellationToken cancellationToken)
    {
        var media = await db.ReportMedias.FirstOrDefaultAsync(item => item.Id == request.MediaId, cancellationToken);
        if (media is null)
        {
            return Result<OpenReportMediaResponse>.Failure(ReportErrors.MediaNotFound());
        }

        if (!request.Signed && media.OwnerUserId != request.ViewerId)
        {
            if (media.ReportId is null || request.ViewerId is null)
            {
                return Result<OpenReportMediaResponse>.Failure(ReportErrors.NotFound());
            }

            var report = await db.Reports.FirstOrDefaultAsync(item => item.Id == media.ReportId, cancellationToken);
            if (report is null || !report.IsVisible(clock.UtcNow))
            {
                return Result<OpenReportMediaResponse>.Failure(ReportErrors.NotFound());
            }

            var friends = await ReportAccess.FriendIdsAsync(db, request.ViewerId.Value, cancellationToken);
            var blocks = await ReportAccess.BlocksAsync(db, request.ViewerId.Value, cancellationToken);
            if (!ReportAccess.CanSee(report, request.ViewerId, friends, blocks))
            {
                return Result<OpenReportMediaResponse>.Failure(ReportErrors.NotFound());
            }

            var category = await db.ReportCategories.FirstOrDefaultAsync(item => item.Id == report.CategoryId, cancellationToken);
            var viewer = await db.Users.FirstOrDefaultAsync(item => item.Id == request.ViewerId, cancellationToken);
            if (category is { IsAdultOnly: true } && (viewer is null || !MinorProtectionPolicy.CanUseAdultOnlyCategories(viewer.AgeOn(clock.UtcNow))))
            {
                return Result<OpenReportMediaResponse>.Failure(ReportErrors.NotFound());
            }
        }

        var key = request.Thumbnail ? media.ThumbnailKey : media.StorageKey;
        if (key is null)
        {
            return Result<OpenReportMediaResponse>.Failure(ReportErrors.MediaNotFound());
        }

        var contentType = media.Type == MediaType.Video ? "video/mp4" : "image/jpeg";
        return Result<OpenReportMediaResponse>.Success(new OpenReportMediaResponse(key, contentType));
    }
}
