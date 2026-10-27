using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Profiles;
using Situ.Application.Features.Reports;
using Situ.Domain.Anonymity;
using Situ.Domain.Common;
using Situ.Domain.Guardianship;
using Situ.Domain.Reports;

namespace Situ.Application.Features.Interaction.Comments;

public sealed record AddCommentRequest(Guid UserId, Guid ReportId, string Text, bool IsAnonymous, Guid? ParentCommentId);

public sealed record AddCommentResponse(Guid Id, string Text, ReportAuthorView Author, DateTimeOffset CreatedAt, int CommentCount);

public sealed class AddCommentValidator : AbstractValidator<AddCommentRequest>
{
    public AddCommentValidator()
    {
        RuleFor(request => request.Text).NotEmpty().MaximumLength(500);
    }
}

public sealed class AddCommentHandler(IApplicationDbContext db, IClock clock)
    : IHandler<AddCommentRequest, Result<AddCommentResponse>>
{
    public async Task<Result<AddCommentResponse>> Handle(AddCommentRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        if (user is null)
        {
            return Result<AddCommentResponse>.Failure(InteractionErrors.NotFound());
        }

        var blocked = InteractionAccess.PublishBlock(user);
        if (blocked is not null)
        {
            return Result<AddCommentResponse>.Failure(blocked);
        }

        var reportResult = await InteractionAccess.OpenReportAsync(db, request.UserId, request.ReportId, true, now, cancellationToken);
        if (reportResult.IsFailure)
        {
            return Result<AddCommentResponse>.Failure(reportResult.Error!);
        }

        var report = reportResult.Value!;
        if (!MinorProtectionPolicy.CanComment(user.AgeOn(now), report.Visibility, request.IsAnonymous))
        {
            return Result<AddCommentResponse>.Failure(InteractionErrors.MinorPublicIdentity());
        }

        if (request.ParentCommentId is { } parentId)
        {
            var parent = await db.Comments.FirstOrDefaultAsync(item => item.Id == parentId, cancellationToken);
            if (parent is null
                || parent.TargetType != ContentTargetType.Report
                || parent.TargetId != report.Id
                || parent.ParentCommentId is not null
                || parent.Status != CommentStatus.Visible)
            {
                return Result<AddCommentResponse>.Failure(InteractionErrors.ParentInvalid());
            }
        }

        int? index = null;
        if (request.IsAnonymous)
        {
            var settings = await ReportSettings.LoadAsync(db, cancellationToken);
            var limit = Math.Max(1, ReportSettings.Int(settings, Domain.Administration.SystemSettingKeys.AnonymousCommentsPerDay, 30));
            var start = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
            var used = await db.Comments.CountAsync(
                item => item.AuthorId == user.Id && item.IsAnonymous && item.CreatedAt >= start,
                cancellationToken);
            if (used >= limit)
            {
                return Result<AddCommentResponse>.Failure(InteractionErrors.AnonymousDailyLimit(start.AddDays(1)));
            }

            index = await AnonymousIndexAllocator.AssignAsync(db, ContentTargetType.Report, report.Id, user.Id, now, cancellationToken);
        }

        var publicationSettings = await ReportSettings.LoadAsync(db, cancellationToken);
        if (ProfileRules.ContainsBlocked(request.Text, ProfileRules.List(publicationSettings, Domain.Administration.SystemSettingKeys.ProfileBlockedTerms)))
        {
            return Result<AddCommentResponse>.Failure(ProfileRules.PublicationRejected());
        }

        var comment = Comment.Create(
            ContentTargetType.Report,
            report.Id,
            user.Id,
            request.Text.Trim(),
            request.IsAnonymous,
            index,
            request.ParentCommentId,
            report.AuthorId,
            now);
        db.Comments.Add(comment);
        report.AddComment();
        await db.SaveChangesAsync(cancellationToken);
        var settingsAfter = await ReportSettings.LoadAsync(db, cancellationToken);
        var profile = request.IsAnonymous
            ? null
            : await db.UserProfiles.FirstOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
        return Result<AddCommentResponse>.Success(new AddCommentResponse(
            comment.Id,
            comment.Text,
            CommentPresentation.Author(comment, user, profile, ReportSettings.DisplayName(settingsAfter)),
            comment.CreatedAt,
            report.CommentCount));
    }
}

public sealed record ListCommentsRequest(Guid? ViewerId, Guid ReportId, string? Cursor, int Limit = 20);

public sealed record CommentItemView(
    Guid Id,
    string Text,
    ReportAuthorView Author,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ReactionCountView> ReactionCounts,
    string? MyReaction,
    IReadOnlyList<CommentItemView> Replies,
    bool IsMine);

public sealed record ListCommentsResponse(IReadOnlyList<CommentItemView> Comments, string? NextCursor);

public sealed class ListCommentsValidator : AbstractValidator<ListCommentsRequest>
{
    public ListCommentsValidator()
    {
        RuleFor(request => request.Limit).InclusiveBetween(1, 50);
    }
}

public sealed class ListCommentsHandler(IApplicationDbContext db, IClock clock)
    : IHandler<ListCommentsRequest, Result<ListCommentsResponse>>
{
    public async Task<Result<ListCommentsResponse>> Handle(ListCommentsRequest request, CancellationToken cancellationToken)
    {
        var reportResult = await InteractionAccess.OpenReportAsync(db, request.ViewerId, request.ReportId, false, clock.UtcNow, cancellationToken);
        if (reportResult.IsFailure)
        {
            return Result<ListCommentsResponse>.Failure(reportResult.Error!);
        }

        var blocks = request.ViewerId is { } viewer
            ? await ReportAccess.BlocksAsync(db, viewer, cancellationToken)
            : ContentBlocks.None;
        var query = db.Comments.Where(item =>
            item.TargetType == ContentTargetType.Report
            && item.TargetId == request.ReportId
            && item.ParentCommentId == null
            && item.Status == CommentStatus.Visible
            && !blocks.Full.Contains(item.AuthorId)
            && !(item.IsAnonymous && blocks.Anonymous.Contains(item.AuthorId)));
        if (Situ.Application.Features.Reports.ListMyReports.ReportCursor.TryDecode(request.Cursor, out var createdAt, out var id))
        {
            query = query.Where(item => item.CreatedAt < createdAt || (item.CreatedAt == createdAt && item.Id.CompareTo(id) < 0));
        }

        var page = await query.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).Take(request.Limit + 1).ToListAsync(cancellationToken);
        var extra = page.Count > request.Limit;
        if (extra)
        {
            page.RemoveAt(page.Count - 1);
        }

        var parentIds = page.Select(item => item.Id).ToArray();
        var replies = await db.Comments
            .Where(item => item.ParentCommentId != null
                && parentIds.Contains(item.ParentCommentId.Value)
                && item.Status == CommentStatus.Visible
                && !blocks.Full.Contains(item.AuthorId)
                && !(item.IsAnonymous && blocks.Anonymous.Contains(item.AuthorId)))
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        var all = page.Concat(replies).ToList();
        var authorIds = all.Select(item => item.AuthorId).Distinct().ToArray();
        var users = await db.Users.Where(item => authorIds.Contains(item.Id)).ToListAsync(cancellationToken);
        var profiles = await db.UserProfiles.Where(item => authorIds.Contains(item.UserId)).ToListAsync(cancellationToken);
        var settings = await ReportSettings.LoadAsync(db, cancellationToken);
        var name = ReportSettings.DisplayName(settings);
        var reactions = await ReactionViews.ForAsync(db, ContentTargetType.Comment, all.Select(item => item.Id).ToArray(), request.ViewerId, cancellationToken);
        CommentItemView Map(Comment comment)
        {
            var bundle = reactions.GetValueOrDefault(comment.Id);
            var children = comment.ParentCommentId is null
                ? replies.Where(item => item.ParentCommentId == comment.Id).Select(Map).ToList()
                : [];
            return new CommentItemView(
                comment.Id,
                comment.Text,
                CommentPresentation.Author(comment, users.FirstOrDefault(item => item.Id == comment.AuthorId), profiles.FirstOrDefault(item => item.UserId == comment.AuthorId), name),
                comment.CreatedAt,
                bundle.Counts ?? [],
                bundle.Mine,
                children,
                request.ViewerId == comment.AuthorId);
        }

        var next = extra && page.Count > 0 ? Situ.Application.Features.Reports.ListMyReports.ReportCursor.Encode(page[^1].CreatedAt, page[^1].Id) : null;
        return Result<ListCommentsResponse>.Success(new ListCommentsResponse(page.Select(Map).ToList(), next));
    }
}

public sealed record EditCommentRequest(Guid UserId, Guid CommentId, string Text);

public sealed record EditCommentResponse(Guid Id, string Text);

public sealed class EditCommentValidator : AbstractValidator<EditCommentRequest>
{
    public EditCommentValidator()
    {
        RuleFor(request => request.Text).NotEmpty().MaximumLength(500);
    }
}

public sealed class EditCommentHandler(IApplicationDbContext db, IClock clock)
    : IHandler<EditCommentRequest, Result<EditCommentResponse>>
{
    public async Task<Result<EditCommentResponse>> Handle(EditCommentRequest request, CancellationToken cancellationToken)
    {
        var comment = await db.Comments.FirstOrDefaultAsync(item => item.Id == request.CommentId, cancellationToken);
        if (comment is null || comment.Status != CommentStatus.Visible)
        {
            return Result<EditCommentResponse>.Failure(InteractionErrors.NotFound());
        }

        if (comment.AuthorId != request.UserId)
        {
            return Result<EditCommentResponse>.Failure(InteractionErrors.NotAuthor());
        }

        var publicationSettings = await ReportSettings.LoadAsync(db, cancellationToken);
        if (ProfileRules.ContainsBlocked(request.Text, ProfileRules.List(publicationSettings, Domain.Administration.SystemSettingKeys.ProfileBlockedTerms)))
        {
            return Result<EditCommentResponse>.Failure(ProfileRules.PublicationRejected());
        }

        try
        {
            comment.Edit(request.Text.Trim(), clock.UtcNow);
        }
        catch (DomainException exception)
        {
            return Result<EditCommentResponse>.Failure(Error.Validation(exception.Code, "Revisa el comentario."));
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result<EditCommentResponse>.Success(new EditCommentResponse(comment.Id, comment.Text));
    }
}

public sealed record DeleteCommentRequest(Guid UserId, Guid CommentId);

public sealed class DeleteCommentHandler(IApplicationDbContext db, IClock clock)
    : IHandler<DeleteCommentRequest, Result>
{
    public async Task<Result> Handle(DeleteCommentRequest request, CancellationToken cancellationToken)
    {
        var comment = await db.Comments.FirstOrDefaultAsync(item => item.Id == request.CommentId, cancellationToken);
        if (comment is null || comment.Status == CommentStatus.Removed)
        {
            return Result.Failure(InteractionErrors.NotFound());
        }

        if (comment.AuthorId != request.UserId)
        {
            return Result.Failure(InteractionErrors.NotAuthor());
        }

        var now = clock.UtcNow;
        await DropVisibleAsync(comment, now, cancellationToken);
        var replies = await db.Comments
            .Where(item => item.ParentCommentId == comment.Id && item.Status == CommentStatus.Visible)
            .ToListAsync(cancellationToken);
        foreach (var reply in replies)
        {
            await DropVisibleAsync(reply, now, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task DropVisibleAsync(Comment comment, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (comment.Status != CommentStatus.Visible)
        {
            comment.Remove(now);
            return;
        }

        comment.Remove(now);
        if (comment.TargetType == ContentTargetType.Report)
        {
            var report = await db.Reports.FirstOrDefaultAsync(item => item.Id == comment.TargetId, cancellationToken);
            report?.RemoveComment();
        }
    }
}
