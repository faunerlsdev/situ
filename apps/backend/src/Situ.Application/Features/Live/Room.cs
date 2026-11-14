using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Interaction;
using Situ.Application.Features.Interaction.Comments;
using Situ.Application.Features.Interaction.Reactions;
using Situ.Application.Features.Moderation;
using Situ.Application.Features.Profiles;
using Situ.Application.Features.Reports;
using Situ.Domain.Administration;
using Situ.Domain.Common;
using Situ.Domain.Guardianship;
using Situ.Domain.Live;
using Situ.Domain.Identity;
using Situ.Domain.Reports;

namespace Situ.Application.Features.Live;

public sealed record ListLiveRoomRequest(Guid UserId, string PublicId);

public sealed record LiveRoomResponse(
    string Title,
    string Visibility,
    int Viewers,
    IReadOnlyList<ReactionCountView> ReactionCounts,
    string? MyReaction,
    IReadOnlyList<CommentItemView> Comments);

public sealed class ListLiveRoomHandler(IApplicationDbContext db, IClock clock)
    : IHandler<ListLiveRoomRequest, Result<LiveRoomResponse>>
{
    public async Task<Result<LiveRoomResponse>> Handle(ListLiveRoomRequest request, CancellationToken cancellationToken)
    {
        var opened = await LiveRoom.OpenAsync(db, request.UserId, request.PublicId, writing: false, clock.UtcNow, cancellationToken);
        if (opened.IsFailure)
        {
            return Result<LiveRoomResponse>.Failure(opened.Error!);
        }

        var stream = opened.Value!;
        var blocks = await ReportAccess.BlocksAsync(db, request.UserId, cancellationToken);
        var withdrawn = db.Users.Where(user => user.Status == UserStatus.DeletionRequested).Select(user => user.Id);
        var comments = await db.Comments
            .Where(item => item.TargetType == ContentTargetType.LiveStream
                && item.TargetId == stream.Id
                && item.ParentCommentId == null
                && item.Status == CommentStatus.Visible
                && !withdrawn.Contains(item.AuthorId)
                && !blocks.Full.Contains(item.AuthorId)
                && !(item.IsAnonymous && blocks.Anonymous.Contains(item.AuthorId)))
            .OrderByDescending(item => item.CreatedAt)
            .Take(40)
            .ToListAsync(cancellationToken);
        var authorIds = comments.Select(item => item.AuthorId).Distinct().ToArray();
        var users = await db.Users.Where(item => authorIds.Contains(item.Id)).ToListAsync(cancellationToken);
        var profiles = await db.UserProfiles.Where(item => authorIds.Contains(item.UserId)).ToListAsync(cancellationToken);
        var settings = await ReportSettings.LoadAsync(db, cancellationToken);
        var name = ReportSettings.DisplayName(settings);
        var commentReactions = await ReactionViews.ForAsync(db, ContentTargetType.Comment, comments.Select(item => item.Id).ToArray(), request.UserId, cancellationToken);
        var views = comments.Select(comment =>
        {
            var bundle = commentReactions.GetValueOrDefault(comment.Id);
            return new CommentItemView(
                comment.Id,
                comment.Text,
                CommentPresentation.Author(comment, users.FirstOrDefault(item => item.Id == comment.AuthorId), profiles.FirstOrDefault(item => item.UserId == comment.AuthorId), name),
                comment.CreatedAt,
                bundle.Counts ?? [],
                bundle.Mine,
                [],
                request.UserId == comment.AuthorId);
        }).ToList();
        var reactions = await ReactionViews.ForAsync(db, ContentTargetType.LiveStream, [stream.Id], request.UserId, cancellationToken);
        var mine = reactions.GetValueOrDefault(stream.Id);
        var viewers = await db.LiveViewerSessions.CountAsync(
            item => item.LiveStreamId == stream.Id && item.ExpiresAt > clock.UtcNow,
            cancellationToken);
        return Result<LiveRoomResponse>.Success(new LiveRoomResponse(
            stream.Title,
            stream.Visibility == ContentVisibility.Public ? "Public" : "Friends",
            viewers,
            mine.Counts ?? [],
            mine.Mine,
            views));
    }
}

public sealed record AddLiveCommentRequest(Guid UserId, string PublicId, string Text, bool IsAnonymous);

public sealed record AddLiveCommentResponse(Guid Id, string Text, ReportAuthorView Author, DateTimeOffset CreatedAt);

public sealed class AddLiveCommentValidator : AbstractValidator<AddLiveCommentRequest>
{
    public AddLiveCommentValidator()
    {
        RuleFor(request => request.Text).NotEmpty().MaximumLength(500);
    }
}

public sealed class AddLiveCommentHandler(IApplicationDbContext db, IClock clock)
    : IHandler<AddLiveCommentRequest, Result<AddLiveCommentResponse>>
{
    public async Task<Result<AddLiveCommentResponse>> Handle(AddLiveCommentRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        if (user is null)
        {
            return Result<AddLiveCommentResponse>.Failure(InteractionErrors.NotFound());
        }

        var blocked = InteractionAccess.PublishBlock(user);
        if (blocked is not null)
        {
            return Result<AddLiveCommentResponse>.Failure(blocked);
        }

        var opened = await LiveRoom.OpenAsync(db, request.UserId, request.PublicId, writing: true, now, cancellationToken);
        if (opened.IsFailure)
        {
            return Result<AddLiveCommentResponse>.Failure(opened.Error!);
        }

        var stream = opened.Value!;
        if (!MinorProtectionPolicy.CanComment(user.AgeOn(now), stream.Visibility, request.IsAnonymous))
        {
            return Result<AddLiveCommentResponse>.Failure(InteractionErrors.MinorPublicIdentity());
        }

        int? index = null;
        if (request.IsAnonymous)
        {
            var settings = await ReportSettings.LoadAsync(db, cancellationToken);
            var limit = Math.Max(1, ReportSettings.Int(settings, SystemSettingKeys.AnonymousCommentsPerDay, 30));
            var start = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
            var used = await db.Comments.CountAsync(
                item => item.AuthorId == user.Id && item.IsAnonymous && item.CreatedAt >= start,
                cancellationToken);
            if (used >= limit)
            {
                return Result<AddLiveCommentResponse>.Failure(InteractionErrors.AnonymousDailyLimit(start.AddDays(1)));
            }

            index = await AnonymousIndexAllocator.AssignAsync(db, ContentTargetType.LiveStream, stream.Id, user.Id, now, cancellationToken);
        }

        var publicationSettings = await ReportSettings.LoadAsync(db, cancellationToken);
        if (ContentFilter.Rejects(request.Text, ContentFilterCatalog.Merge(ContentFilterCatalog.Current(), ProfileRules.List(publicationSettings, SystemSettingKeys.ProfileBlockedTerms))))
        {
            return Result<AddLiveCommentResponse>.Failure(ContentFilter.Rejected("text"));
        }

        var comment = Comment.Create(
            ContentTargetType.LiveStream,
            stream.Id,
            user.Id,
            request.Text.Trim(),
            request.IsAnonymous,
            index,
            null,
            stream.StreamerId,
            now);
        db.Comments.Add(comment);
        await db.SaveChangesAsync(cancellationToken);
        var profile = request.IsAnonymous
            ? null
            : await db.UserProfiles.FirstOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
        return Result<AddLiveCommentResponse>.Success(new AddLiveCommentResponse(
            comment.Id,
            comment.Text,
            CommentPresentation.Author(comment, user, profile, ReportSettings.DisplayName(publicationSettings)),
            comment.CreatedAt));
    }
}

public sealed record SetLiveReactionRequest(Guid UserId, string PublicId, string? Type);

public sealed class SetLiveReactionHandler(IApplicationDbContext db, IClock clock)
    : IHandler<SetLiveReactionRequest, Result<SetReactionResponse>>
{
    public async Task<Result<SetReactionResponse>> Handle(SetLiveReactionRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var opened = await LiveRoom.OpenAsync(db, request.UserId, request.PublicId, writing: true, now, cancellationToken);
        if (opened.IsFailure)
        {
            return Result<SetReactionResponse>.Failure(opened.Error!);
        }

        var stream = opened.Value!;
        var current = await db.Reactions.FirstOrDefaultAsync(
            item => item.TargetType == ContentTargetType.LiveStream && item.TargetId == stream.Id && item.UserId == request.UserId,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Type))
        {
            if (current is null)
            {
                return Result<SetReactionResponse>.Failure(InteractionErrors.ReactionNotFound());
            }

            db.Reactions.Remove(current);
        }
        else if (!Enum.TryParse<ReactionType>(request.Type, out var type))
        {
            return Result<SetReactionResponse>.Failure(Error.Validation("reaction.invalid", "Elige una reacción válida."));
        }
        else if (current is null)
        {
            db.Reactions.Add(Reaction.Create(ContentTargetType.LiveStream, stream.Id, request.UserId, type, now));
        }
        else
        {
            current.Change(type, now);
        }

        await db.SaveChangesAsync(cancellationToken);
        var bundle = await ReactionViews.ForAsync(db, ContentTargetType.LiveStream, [stream.Id], request.UserId, cancellationToken);
        var view = bundle.GetValueOrDefault(stream.Id);
        return Result<SetReactionResponse>.Success(new SetReactionResponse(request.Type ?? string.Empty, view.Counts ?? []));
    }
}

internal static class LiveRoom
{
    public static async Task<Result<LiveStream>> OpenAsync(
        IApplicationDbContext db,
        Guid userId,
        string publicId,
        bool writing,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var viewer = await db.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        var stream = await db.LiveStreams.FirstOrDefaultAsync(item => item.PublicPath == publicId, cancellationToken);
        if (viewer is null || stream is null)
        {
            return Result<LiveStream>.Failure(LiveRules.NotFound());
        }

        var settings = await LiveRules.SettingsAsync(db, cancellationToken);
        if (!await ListLiveHandler.VisibleAsync(db, viewer, stream, settings, now, cancellationToken, liveOnly: false))
        {
            return Result<LiveStream>.Failure(LiveRules.NotFound());
        }

        if (writing && stream.Status != LiveStreamStatus.Live)
        {
            return Result<LiveStream>.Failure(Error.Conflict("live.not_live", "Esta transmisión ya no admite comentarios."));
        }

        return Result<LiveStream>.Success(stream);
    }
}
