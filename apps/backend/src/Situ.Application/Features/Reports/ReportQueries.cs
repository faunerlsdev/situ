using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Interaction;
using Situ.Domain.Common;
using Situ.Domain.Guardianship;
using Situ.Domain.Reports;

namespace Situ.Application.Features.Reports.GetReport
{

public sealed record GetReportRequest(Guid? ViewerId, Guid ReportId);

public sealed record GetReportResponse(
    ReportCardView Report,
    IReadOnlyList<ReportMediaView> Media,
    string? MyConfirmation,
    IReadOnlyList<ReportCommentView> Comments);

public sealed class GetReportHandler(IApplicationDbContext db, IClock clock, IMediaUrlSigner urls)
    : IHandler<GetReportRequest, Result<GetReportResponse>>
{
    public async Task<Result<GetReportResponse>> Handle(GetReportRequest request, CancellationToken cancellationToken)
    {
        var report = await db.Reports.FirstOrDefaultAsync(item => item.Id == request.ReportId, cancellationToken);
        if (report is null)
        {
            return Result<GetReportResponse>.Failure(ReportErrors.NotFound());
        }

        if (UnavailableStatus(report, clock.UtcNow) is { } unavailable && request.ViewerId != report.AuthorId)
        {
            return Result<GetReportResponse>.Failure(ReportErrors.NotAvailable(unavailable));
        }

        var friends = request.ViewerId is { } viewer
            ? await ReportAccess.FriendIdsAsync(db, viewer, cancellationToken)
            : [];
        var blocks = request.ViewerId is { } blockedViewer
            ? await ReportAccess.BlocksAsync(db, blockedViewer, cancellationToken)
            : ContentBlocks.None;
        var isAuthor = request.ViewerId == report.AuthorId;
        if (!isAuthor && (!report.IsVisible(clock.UtcNow) || !ReportAccess.CanSee(report, request.ViewerId, friends, blocks)))
        {
            return Result<GetReportResponse>.Failure(ReportErrors.NotFound());
        }

        if (!isAuthor && request.ViewerId is { } userId)
        {
            var user = await db.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
            var adultCategory = await db.ReportCategories.FirstOrDefaultAsync(item => item.Id == report.CategoryId, cancellationToken);
            if (adultCategory is { IsAdultOnly: true } && (user is null || !MinorProtectionPolicy.CanUseAdultOnlyCategories(user.AgeOn(clock.UtcNow))))
            {
                return Result<GetReportResponse>.Failure(ReportErrors.NotFound());
            }
        }

        var settings = await ReportSettings.LoadAsync(db, cancellationToken);
        var author = await db.Users.FirstOrDefaultAsync(item => item.Id == report.AuthorId, cancellationToken);
        var profile = report.IsAnonymous
            ? null
            : await db.UserProfiles.FirstOrDefaultAsync(item => item.UserId == report.AuthorId, cancellationToken);
        var media = await db.ReportMedias
            .Where(item => item.ReportId == report.Id && item.ProcessingStatus == MediaProcessingStatus.Ready)
            .OrderBy(item => item.SortOrder)
            .ToListAsync(cancellationToken);
        var now = clock.UtcNow;
        var mediaViews = media.Select(item =>
        {
            var signed = urls.Sign(item.Id, now.AddMinutes(15));
            var thumb = item.ThumbnailKey is null ? null : ReportPresentation.ReadUrl(item.Id, signed.ExpiresUnix, signed.Signature) + "&kind=thumb";
            return new ReportMediaView(item.Id, item.Type.ToString(), ReportPresentation.ReadUrl(item.Id, signed.ExpiresUnix, signed.Signature), thumb, item.DurationSeconds);
        }).ToList();
        var vote = request.ViewerId is { } voter
            ? await db.ReportConfirmations.FirstOrDefaultAsync(item => item.ReportId == report.Id && item.UserId == voter, cancellationToken)
            : null;
        var comments = await db.Comments
            .Where(item => item.TargetType == ContentTargetType.Report
                && item.TargetId == report.Id
                && item.Status == CommentStatus.Visible
                && !blocks.Full.Contains(item.AuthorId)
                && !(item.IsAnonymous && blocks.Anonymous.Contains(item.AuthorId)))
            .OrderByDescending(item => item.CreatedAt)
            .Take(3)
            .ToListAsync(cancellationToken);
        var commentAuthors = comments.Select(item => item.AuthorId).Distinct().ToArray();
        var users = await db.Users.Where(item => commentAuthors.Contains(item.Id)).ToListAsync(cancellationToken);
        var profiles = await db.UserProfiles.Where(item => commentAuthors.Contains(item.UserId)).ToListAsync(cancellationToken);
        var commentReactions = await ReactionViews.ForAsync(db, ContentTargetType.Comment, comments.Select(item => item.Id).ToArray(), request.ViewerId, cancellationToken);
        var commentViews = comments.Select(comment =>
        {
            var commentUser = users.FirstOrDefault(item => item.Id == comment.AuthorId);
            var commentProfile = comment.IsAnonymous ? null : profiles.FirstOrDefault(item => item.UserId == comment.AuthorId);
            var bundle = commentReactions.GetValueOrDefault(comment.Id);
            return new ReportCommentView(
                comment.Id,
                comment.Text,
                CommentPresentation.Author(comment, commentUser, commentProfile, ReportSettings.DisplayName(settings)),
                comment.CreatedAt,
                bundle.Counts ?? [],
                bundle.Mine);
        }).ToList();
        var reportReactions = await ReactionViews.ForAsync(db, ContentTargetType.Report, [report.Id], request.ViewerId, cancellationToken);
        var reportBundle = reportReactions.GetValueOrDefault(report.Id);
        var category = await db.ReportCategories.FirstOrDefaultAsync(item => item.Id == report.CategoryId, cancellationToken);
        var card = new ReportCardView(
            report.Id,
            report.CategoryId,
            category?.Code,
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
            mediaViews.FirstOrDefault()?.ThumbnailUrl ?? mediaViews.FirstOrDefault()?.Url,
            reportBundle.Counts ?? [],
            reportBundle.Mine);
        return Result<GetReportResponse>.Success(new GetReportResponse(card, mediaViews, vote?.Type.ToString(), commentViews));
    }

    private static string? UnavailableStatus(Report report, DateTimeOffset now)
    {
        if (report.Status is ReportStatus.Expired or ReportStatus.Removed or ReportStatus.UnderReview)
        {
            return report.Status.ToString();
        }

        if (report.Status == ReportStatus.Active && now >= report.ExpiresAt)
        {
            return nameof(ReportStatus.Expired);
        }

        return null;
    }
}

}

namespace Situ.Application.Features.Reports.ListMyReports
{

public sealed record ListMyReportsRequest(Guid UserId, string? Cursor, int Limit = 20);

public sealed record ListMyReportsResponse(IReadOnlyList<ReportCardView> Reports, string? NextCursor);

public sealed class ListMyReportsValidator : AbstractValidator<ListMyReportsRequest>
{
    public ListMyReportsValidator()
    {
        RuleFor(request => request.Limit).InclusiveBetween(1, 50);
    }
}

public sealed class ListMyReportsHandler(IApplicationDbContext db, IMediaUrlSigner urls, IClock clock)
    : IHandler<ListMyReportsRequest, Result<ListMyReportsResponse>>
{
    public async Task<Result<ListMyReportsResponse>> Handle(ListMyReportsRequest request, CancellationToken cancellationToken)
    {
        var query = db.Reports.Where(report => report.AuthorId == request.UserId);
        if (ReportCursor.TryDecode(request.Cursor, out var createdAt, out var id))
        {
            query = query.Where(report => report.CreatedAt < createdAt || (report.CreatedAt == createdAt && report.Id.CompareTo(id) < 0));
        }

        var page = await query.OrderByDescending(report => report.CreatedAt).ThenByDescending(report => report.Id).Take(request.Limit + 1).ToListAsync(cancellationToken);
        var extra = page.Count > request.Limit;
        if (extra)
        {
            page.RemoveAt(page.Count - 1);
        }

        var settings = await ReportSettings.LoadAsync(db, cancellationToken);
        var user = await db.Users.FirstAsync(item => item.Id == request.UserId, cancellationToken);
        var mineReactions = await Situ.Application.Features.Interaction.ReactionViews.ForAsync(
            db,
            ContentTargetType.Report,
            page.Select(item => item.Id).ToArray(),
            request.UserId,
            cancellationToken);
        var profile = await db.UserProfiles.FirstOrDefaultAsync(item => item.UserId == request.UserId, cancellationToken);
        var cards = new List<ReportCardView>();
        foreach (var report in page)
        {
            cards.Add(new ReportCardView(
                report.Id,
                report.CategoryId,
                null,
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
                ReportPresentation.Author(report.IsAnonymous, user, profile, ReportSettings.DisplayName(settings)),
                null,
                mineReactions.GetValueOrDefault(report.Id).Counts ?? [],
                mineReactions.GetValueOrDefault(report.Id).Mine));
        }

        _ = urls;
        _ = clock;
        var next = extra && page.Count > 0 ? ReportCursor.Encode(page[^1].CreatedAt, page[^1].Id) : null;
        return Result<ListMyReportsResponse>.Success(new ListMyReportsResponse(cards, next));
    }
}

internal static class ReportCursor
{
    public static string Encode(DateTimeOffset createdAt, Guid id) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{createdAt.UtcDateTime:O}|{id}"));

    public static bool TryDecode(string? cursor, out DateTimeOffset createdAt, out Guid id)
    {
        createdAt = default;
        id = default;
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return false;
        }

        try
        {
            var text = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = text.Split('|', 2);
            return parts.Length == 2 && DateTimeOffset.TryParse(parts[0], out createdAt) && Guid.TryParse(parts[1], out id);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
}
