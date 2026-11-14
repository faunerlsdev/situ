using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Interaction;
using Situ.Application.Features.Reports.ListMyReports;
using Situ.Domain.Common;
using Situ.Domain.Guardianship;
using Situ.Domain.Identity;
using Situ.Domain.Reports;

namespace Situ.Application.Features.Reports.ListReportFeed;

public sealed record ListReportFeedRequest(Guid UserId, string? Cursor, int Limit = 20);

public sealed record ListReportFeedResponse(IReadOnlyList<ReportCardView> Reports, string? NextCursor);

public sealed class ListReportFeedValidator : AbstractValidator<ListReportFeedRequest>
{
    public ListReportFeedValidator()
    {
        RuleFor(request => request.Limit).InclusiveBetween(1, 50);
    }
}

/// <summary>
/// Reports that are still kept, including those that already left the map.
/// Removed and under-review rows stay out.
/// </summary>
public sealed class ListReportFeedHandler(IApplicationDbContext db, IMediaUrlSigner urls, IClock clock)
    : IHandler<ListReportFeedRequest, Result<ListReportFeedResponse>>
{
    public async Task<Result<ListReportFeedResponse>> Handle(ListReportFeedRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var viewer = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        if (viewer is null)
        {
            return Result<ListReportFeedResponse>.Failure(ReportErrors.NotFound());
        }

        var friends = (await ReportAccess.FriendIdsAsync(db, request.UserId, cancellationToken)).ToArray();
        var blocks = await ReportAccess.BlocksAsync(db, request.UserId, cancellationToken);
        var blocked = blocks.Full.ToArray();
        var anonymousBlocked = blocks.Anonymous.ToArray();
        var minor = viewer.IsMinor(now);
        var withdrawn = db.Users.Where(user => user.Status == UserStatus.DeletionRequested).Select(user => user.Id);
        var query = db.Reports.Where(report =>
            (report.Status == ReportStatus.Active || report.Status == ReportStatus.Expired)
            && !withdrawn.Contains(report.AuthorId)
            && !blocked.Contains(report.AuthorId)
            && !(report.IsAnonymous && anonymousBlocked.Contains(report.AuthorId))
            && (report.Visibility == ContentVisibility.Public
                || report.AuthorId == request.UserId
                || friends.Contains(report.AuthorId)));
        if (minor)
        {
            var adultIds = db.ReportCategories.Where(item => item.IsAdultOnly).Select(item => item.Id);
            query = query.Where(report => !adultIds.Contains(report.CategoryId));
        }

        if (ReportCursor.TryDecode(request.Cursor, out var createdAt, out var id))
        {
            query = query.Where(report => report.CreatedAt < createdAt || (report.CreatedAt == createdAt && report.Id.CompareTo(id) < 0));
        }

        var page = await query
            .OrderByDescending(report => report.CreatedAt)
            .ThenByDescending(report => report.Id)
            .Take(request.Limit + 1)
            .ToListAsync(cancellationToken);
        var extra = page.Count > request.Limit;
        if (extra)
        {
            page.RemoveAt(page.Count - 1);
        }

        var reportIds = page.Select(item => item.Id).ToArray();
        var authorIds = page.Select(item => item.AuthorId).Distinct().ToArray();
        var categoryIds = page.Select(item => item.CategoryId).Distinct().ToArray();
        var media = await db.ReportMedias
            .Where(item => item.ReportId != null && reportIds.Contains(item.ReportId.Value) && item.ProcessingStatus == MediaProcessingStatus.Ready)
            .OrderBy(item => item.SortOrder)
            .ToListAsync(cancellationToken);
        var users = await db.Users.Where(item => authorIds.Contains(item.Id)).ToListAsync(cancellationToken);
        var profiles = await db.UserProfiles.Where(item => authorIds.Contains(item.UserId)).ToListAsync(cancellationToken);
        var categories = await db.ReportCategories.Where(item => categoryIds.Contains(item.Id)).ToListAsync(cancellationToken);
        var settings = await ReportSettings.LoadAsync(db, cancellationToken);
        var reactions = await ReactionViews.ForAsync(db, ContentTargetType.Report, reportIds, request.UserId, cancellationToken);
        var cards = new List<ReportCardView>();
        foreach (var report in page)
        {
            var file = media.FirstOrDefault(item => item.ReportId == report.Id);
            string? preview = null;
            if (file is not null)
            {
                var signed = urls.Sign(file.Id, now.AddMinutes(15));
                preview = file.ThumbnailKey is null
                    ? ReportPresentation.ReadUrl(file.Id, signed.ExpiresUnix, signed.Signature)
                    : ReportPresentation.ReadUrl(file.Id, signed.ExpiresUnix, signed.Signature) + "&kind=thumb";
            }

            var author = users.FirstOrDefault(item => item.Id == report.AuthorId);
            var profile = report.IsAnonymous ? null : profiles.FirstOrDefault(item => item.UserId == report.AuthorId);
            var bundle = reactions.GetValueOrDefault(report.Id);
            cards.Add(new ReportCardView(
                report.Id,
                report.CategoryId,
                categories.FirstOrDefault(item => item.Id == report.CategoryId)?.Code,
                report.Description,
                report.Location.Latitude,
                report.Location.Longitude,
                report.Visibility.ToString(),
                report.IsAnonymous,
                report.HideAuthorOnMap,
                report.Status.ToString(),
                report.CreatedAt,
                report.ExpiresAt,
                report.StillThereCount,
                report.GoneNowCount,
                report.CommentCount,
                report.ReactionCount,
                ReportPresentation.Author(report.IsAnonymous, author, profile, ReportSettings.DisplayName(settings)),
                preview,
                bundle.Counts ?? [],
                bundle.Mine));
        }

        var next = extra && page.Count > 0
            ? ReportCursor.Encode(page[^1].CreatedAt, page[^1].Id)
            : null;
        return Result<ListReportFeedResponse>.Success(new ListReportFeedResponse(cards, next));
    }
}
