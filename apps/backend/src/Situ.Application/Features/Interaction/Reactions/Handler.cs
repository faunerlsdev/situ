using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Reports;
using Situ.Domain.Common;
using Situ.Domain.Reports;

namespace Situ.Application.Features.Interaction.Reactions;

public sealed record SetReactionRequest(Guid UserId, ContentTargetType TargetType, Guid TargetId, ReactionType Type);

public sealed record SetReactionResponse(string Type, IReadOnlyList<ReactionCountView> Counts);

public sealed class SetReactionValidator : AbstractValidator<SetReactionRequest>
{
    public SetReactionValidator()
    {
        RuleFor(request => request.TargetType).Must(type => type is ContentTargetType.Report or ContentTargetType.Comment);
    }
}

public sealed class SetReactionHandler(IApplicationDbContext db, IClock clock)
    : IHandler<SetReactionRequest, Result<SetReactionResponse>>
{
    public async Task<Result<SetReactionResponse>> Handle(SetReactionRequest request, CancellationToken cancellationToken)
    {
        var gate = await ReactionTarget.EnsureAsync(db, request.UserId, request.TargetType, request.TargetId, clock.UtcNow, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<SetReactionResponse>.Failure(gate.Error!);
        }

        var now = clock.UtcNow;
        var current = await db.Reactions.FirstOrDefaultAsync(
            item => item.TargetType == request.TargetType && item.TargetId == request.TargetId && item.UserId == request.UserId,
            cancellationToken);
        if (current is null)
        {
            db.Reactions.Add(Reaction.Create(request.TargetType, request.TargetId, request.UserId, request.Type, now));
        }
        else
        {
            current.Change(request.Type, now);
        }

        if (gate.Value is { } reportId)
        {
            await db.SaveChangesAsync(cancellationToken);
            await ReactionViews.SyncReportCountAsync(db, reportId, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        var bundle = await ReactionViews.ForAsync(db, request.TargetType, [request.TargetId], request.UserId, cancellationToken);
        var view = bundle.GetValueOrDefault(request.TargetId);
        return Result<SetReactionResponse>.Success(new SetReactionResponse(request.Type.ToString(), view.Counts ?? []));
    }
}

public sealed record RemoveReactionRequest(Guid UserId, ContentTargetType TargetType, Guid TargetId);

public sealed class RemoveReactionHandler(IApplicationDbContext db, IClock clock)
    : IHandler<RemoveReactionRequest, Result<SetReactionResponse>>
{
    public async Task<Result<SetReactionResponse>> Handle(RemoveReactionRequest request, CancellationToken cancellationToken)
    {
        var gate = await ReactionTarget.EnsureAsync(db, request.UserId, request.TargetType, request.TargetId, clock.UtcNow, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<SetReactionResponse>.Failure(gate.Error!);
        }

        var current = await db.Reactions.FirstOrDefaultAsync(
            item => item.TargetType == request.TargetType && item.TargetId == request.TargetId && item.UserId == request.UserId,
            cancellationToken);
        if (current is null)
        {
            return Result<SetReactionResponse>.Failure(InteractionErrors.ReactionNotFound());
        }

        db.Reactions.Remove(current);
        await db.SaveChangesAsync(cancellationToken);
        if (gate.Value is { } reportId)
        {
            await ReactionViews.SyncReportCountAsync(db, reportId, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        var bundle = await ReactionViews.ForAsync(db, request.TargetType, [request.TargetId], request.UserId, cancellationToken);
        var view = bundle.GetValueOrDefault(request.TargetId);
        return Result<SetReactionResponse>.Success(new SetReactionResponse(string.Empty, view.Counts ?? []));
    }
}

internal static class ReactionTarget
{
    public static async Task<Result<Guid?>> EnsureAsync(
        IApplicationDbContext db,
        Guid userId,
        ContentTargetType targetType,
        Guid targetId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return Result<Guid?>.Failure(InteractionErrors.CannotPublish());
        }

        var blocked = InteractionAccess.PublishBlock(user);
        if (blocked is not null)
        {
            return Result<Guid?>.Failure(blocked);
        }

        if (targetType == ContentTargetType.Report)
        {
            var report = await InteractionAccess.OpenReportAsync(db, userId, targetId, true, now, cancellationToken);
            return report.IsFailure ? Result<Guid?>.Failure(report.Error!) : Result<Guid?>.Success(report.Value!.Id);
        }

        var comment = await db.Comments.FirstOrDefaultAsync(item => item.Id == targetId && item.Status == CommentStatus.Visible, cancellationToken);
        if (comment is null || comment.TargetType != ContentTargetType.Report)
        {
            return Result<Guid?>.Failure(InteractionErrors.NotFound());
        }

        var host = await InteractionAccess.OpenReportAsync(db, userId, comment.TargetId, true, now, cancellationToken);
        if (host.IsFailure)
        {
            return Result<Guid?>.Failure(host.Error!);
        }

        if (await InteractionAccess.HidesAuthorAsync(db, userId, comment.AuthorId, comment.IsAnonymous, cancellationToken))
        {
            return Result<Guid?>.Failure(InteractionErrors.NotFound());
        }

        return Result<Guid?>.Success(null);
    }
}
