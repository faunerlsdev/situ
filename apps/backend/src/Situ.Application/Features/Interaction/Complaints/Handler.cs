using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Reports;
using Situ.Domain.Administration;
using Situ.Domain.Common;
using Situ.Domain.Moderation;
using Situ.Domain.Reports;

namespace Situ.Application.Features.Interaction.Complaints;

public sealed record FileComplaintRequest(Guid UserId, ContentTargetType TargetType, Guid TargetId, ComplaintReason Reason, string? Details);

public sealed record FileComplaintResponse(Guid Id, string Status, bool IsPriority);

public sealed class FileComplaintValidator : AbstractValidator<FileComplaintRequest>
{
    public FileComplaintValidator()
    {
        RuleFor(request => request.TargetType).Must(type => type is ContentTargetType.Report or ContentTargetType.Comment or ContentTargetType.User);
        RuleFor(request => request.Details).MaximumLength(1000);
    }
}

public sealed class FileComplaintHandler(IApplicationDbContext db, IClock clock)
    : IHandler<FileComplaintRequest, Result<FileComplaintResponse>>
{
    public async Task<Result<FileComplaintResponse>> Handle(FileComplaintRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        if (user is null)
        {
            return Result<FileComplaintResponse>.Failure(InteractionErrors.CannotPublish());
        }

        var publish = InteractionAccess.PublishBlock(user);
        if (publish is not null)
        {
            return Result<FileComplaintResponse>.Failure(publish);
        }

        var authorId = await AuthorOfAsync(request.TargetType, request.TargetId, cancellationToken);
        if (authorId is null)
        {
            return Result<FileComplaintResponse>.Failure(InteractionErrors.ComplaintTarget());
        }

        if (authorId == request.UserId)
        {
            return Result<FileComplaintResponse>.Failure(InteractionErrors.ComplaintSelf());
        }

        var duplicate = await db.Complaints.AnyAsync(
            item => item.ReporterId == request.UserId
                && item.TargetType == request.TargetType
                && item.TargetId == request.TargetId
                && (item.Status == ComplaintStatus.Open || item.Status == ComplaintStatus.InReview),
            cancellationToken);
        if (duplicate)
        {
            return Result<FileComplaintResponse>.Failure(InteractionErrors.ComplaintDuplicate());
        }

        var complaint = Complaint.File(request.UserId, request.TargetType, request.TargetId, request.Reason, request.Details, now);
        db.Complaints.Add(complaint);
        await db.SaveChangesAsync(cancellationToken);
        await ComplaintEffects.ApplyAutomaticReviewAsync(db, complaint, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Result<FileComplaintResponse>.Success(new FileComplaintResponse(complaint.Id, complaint.Status.ToString(), complaint.IsPriority));
    }

    private async Task<Guid?> AuthorOfAsync(ContentTargetType targetType, Guid targetId, CancellationToken cancellationToken)
    {
        if (targetType == ContentTargetType.Report)
        {
            return await db.Reports.Where(item => item.Id == targetId).Select(item => (Guid?)item.AuthorId).FirstOrDefaultAsync(cancellationToken);
        }

        if (targetType == ContentTargetType.Comment)
        {
            return await db.Comments.Where(item => item.Id == targetId).Select(item => (Guid?)item.AuthorId).FirstOrDefaultAsync(cancellationToken);
        }

        var exists = await db.Users.AnyAsync(item => item.Id == targetId, cancellationToken);
        return exists ? targetId : null;
    }
}

public sealed record ListComplaintsRequest(Guid StaffId, string? Status, string? Reason);

public sealed record ComplaintQueueItem(
    Guid Id,
    string TargetType,
    Guid TargetId,
    string Reason,
    string Status,
    bool IsPriority,
    DateTimeOffset CreatedAt,
    string ReporterName);

public sealed record ListComplaintsResponse(IReadOnlyList<ComplaintQueueItem> Complaints);

public sealed class ListComplaintsHandler(IApplicationDbContext db)
    : IHandler<ListComplaintsRequest, Result<ListComplaintsResponse>>
{
    public async Task<Result<ListComplaintsResponse>> Handle(ListComplaintsRequest request, CancellationToken cancellationToken)
    {
        if (!await StaffGate.HasAsync(db, request.StaffId, StaffPermissionCodes.ComplaintsResolve, cancellationToken))
        {
            return Result<ListComplaintsResponse>.Failure(InteractionErrors.StaffForbidden());
        }

        var query = db.Complaints.AsQueryable();
        if (Enum.TryParse<ComplaintStatus>(request.Status, out var status))
        {
            query = query.Where(item => item.Status == status);
        }

        if (Enum.TryParse<ComplaintReason>(request.Reason, out var reason))
        {
            query = query.Where(item => item.Reason == reason);
        }

        var rows = await query
            .OrderByDescending(item => item.IsPriority)
            .ThenBy(item => item.Reason == ComplaintReason.MinorSafety ? 0 : 1)
            .ThenBy(item => item.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);
        var reporterIds = rows.Select(item => item.ReporterId).Distinct().ToArray();
        var users = await db.Users.Where(item => reporterIds.Contains(item.Id)).ToListAsync(cancellationToken);
        var profiles = await db.UserProfiles.Where(item => reporterIds.Contains(item.UserId)).ToListAsync(cancellationToken);
        var items = rows.Select(row =>
        {
            var user = users.FirstOrDefault(item => item.Id == row.ReporterId);
            var profile = profiles.FirstOrDefault(item => item.UserId == row.ReporterId);
            return new ComplaintQueueItem(
                row.Id,
                row.TargetType.ToString(),
                row.TargetId,
                row.Reason.ToString(),
                row.Status.ToString(),
                row.IsPriority,
                row.CreatedAt,
                profile?.DisplayName ?? user?.UserName.Value ?? "Usuario");
        }).ToList();
        return Result<ListComplaintsResponse>.Success(new ListComplaintsResponse(items));
    }
}

public sealed record StartComplaintReviewRequest(Guid StaffId, Guid ComplaintId);

public sealed class StartComplaintReviewHandler(IApplicationDbContext db, IClock clock)
    : IHandler<StartComplaintReviewRequest, Result>
{
    public async Task<Result> Handle(StartComplaintReviewRequest request, CancellationToken cancellationToken)
    {
        if (!await StaffGate.HasAsync(db, request.StaffId, StaffPermissionCodes.ComplaintsResolve, cancellationToken))
        {
            return Result.Failure(InteractionErrors.StaffForbidden());
        }

        var complaint = await db.Complaints.FirstOrDefaultAsync(item => item.Id == request.ComplaintId, cancellationToken);
        if (complaint is null)
        {
            return Result.Failure(InteractionErrors.ComplaintNotFound());
        }

        var now = clock.UtcNow;
        try
        {
            complaint.StartReview(request.StaffId, now);
        }
        catch (DomainException exception)
        {
            return Result.Failure(Error.Validation(exception.Code, "La denuncia no está abierta."));
        }

        Audit.Write(db, request.StaffId, "complaint.start_review", "Complaint", complaint.Id, null, now);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record UpholdComplaintRequest(Guid StaffId, Guid ComplaintId, string Resolution, string Action);

public sealed class UpholdComplaintValidator : AbstractValidator<UpholdComplaintRequest>
{
    public UpholdComplaintValidator()
    {
        RuleFor(request => request.Resolution).NotEmpty().MaximumLength(1000);
        RuleFor(request => request.Action).Must(action => action is "Remove" or "RemoveAndSuspend");
    }
}

public sealed class UpholdComplaintHandler(IApplicationDbContext db, IClock clock)
    : IHandler<UpholdComplaintRequest, Result>
{
    public async Task<Result> Handle(UpholdComplaintRequest request, CancellationToken cancellationToken)
    {
        if (!await StaffGate.HasAsync(db, request.StaffId, StaffPermissionCodes.ComplaintsResolve, cancellationToken))
        {
            return Result.Failure(InteractionErrors.StaffForbidden());
        }

        var suspend = request.Action == "RemoveAndSuspend";
        if (suspend && !await StaffGate.HasAsync(db, request.StaffId, StaffPermissionCodes.UsersSuspend, cancellationToken))
        {
            return Result.Failure(InteractionErrors.SuspendForbidden());
        }

        var complaint = await db.Complaints.FirstOrDefaultAsync(item => item.Id == request.ComplaintId, cancellationToken);
        if (complaint is null)
        {
            return Result.Failure(InteractionErrors.ComplaintNotFound());
        }

        var now = clock.UtcNow;
        Guid? authorId = await ContentAuthorAsync(complaint, cancellationToken);
        try
        {
            complaint.Uphold(request.StaffId, request.Resolution.Trim(), now);
        }
        catch (DomainException exception)
        {
            return Result.Failure(Error.Validation(exception.Code, "La denuncia ya estaba resuelta."));
        }

        await RemoveContentAsync(complaint, now, cancellationToken);
        if (authorId is { } author)
        {
            await ComplaintEffects.PenalizeAuthorAsync(db, author, now, cancellationToken);
            if (suspend)
            {
                var target = await db.Users.FirstOrDefaultAsync(item => item.Id == author, cancellationToken);
                target?.Suspend(null);
                db.ModerationActions.Add(ModerationAction.Record(request.StaffId, ModerationActionType.SuspendUser, ContentTargetType.User, author, complaint.Id, request.Resolution.Trim(), now));
            }
        }

        db.ModerationActions.Add(ModerationAction.Record(
            request.StaffId,
            ModerationActionType.RemoveContent,
            complaint.TargetType,
            complaint.TargetId,
            complaint.Id,
            request.Resolution.Trim(),
            now));
        Audit.Write(db, request.StaffId, "complaint.uphold", "Complaint", complaint.Id, request.Action, now);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Guid?> ContentAuthorAsync(Complaint complaint, CancellationToken cancellationToken)
    {
        if (complaint.TargetType == ContentTargetType.Report)
        {
            return await db.Reports.Where(item => item.Id == complaint.TargetId).Select(item => (Guid?)item.AuthorId).FirstOrDefaultAsync(cancellationToken);
        }

        if (complaint.TargetType == ContentTargetType.Comment)
        {
            return await db.Comments.Where(item => item.Id == complaint.TargetId).Select(item => (Guid?)item.AuthorId).FirstOrDefaultAsync(cancellationToken);
        }

        return complaint.TargetType == ContentTargetType.User ? complaint.TargetId : null;
    }

    private async Task RemoveContentAsync(Complaint complaint, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (complaint.TargetType == ContentTargetType.Report)
        {
            var report = await db.Reports.FirstOrDefaultAsync(item => item.Id == complaint.TargetId, cancellationToken);
            report?.Remove(now);
            return;
        }

        if (complaint.TargetType != ContentTargetType.Comment)
        {
            return;
        }

        var comment = await db.Comments.FirstOrDefaultAsync(item => item.Id == complaint.TargetId, cancellationToken);
        if (comment is null)
        {
            return;
        }

        var wasVisible = comment.Status == CommentStatus.Visible;
        comment.Remove(now);
        if (wasVisible && comment.TargetType == ContentTargetType.Report)
        {
            var report = await db.Reports.FirstOrDefaultAsync(item => item.Id == comment.TargetId, cancellationToken);
            report?.RemoveComment();
        }
    }
}

public sealed record DismissComplaintRequest(Guid StaffId, Guid ComplaintId, string Resolution);

public sealed class DismissComplaintValidator : AbstractValidator<DismissComplaintRequest>
{
    public DismissComplaintValidator()
    {
        RuleFor(request => request.Resolution).NotEmpty().MaximumLength(1000);
    }
}

public sealed class DismissComplaintHandler(IApplicationDbContext db, IClock clock)
    : IHandler<DismissComplaintRequest, Result>
{
    public async Task<Result> Handle(DismissComplaintRequest request, CancellationToken cancellationToken)
    {
        if (!await StaffGate.HasAsync(db, request.StaffId, StaffPermissionCodes.ComplaintsResolve, cancellationToken))
        {
            return Result.Failure(InteractionErrors.StaffForbidden());
        }

        var complaint = await db.Complaints.FirstOrDefaultAsync(item => item.Id == request.ComplaintId, cancellationToken);
        if (complaint is null)
        {
            return Result.Failure(InteractionErrors.ComplaintNotFound());
        }

        var now = clock.UtcNow;
        try
        {
            complaint.Dismiss(request.StaffId, request.Resolution.Trim(), now);
        }
        catch (DomainException exception)
        {
            return Result.Failure(Error.Validation(exception.Code, "La denuncia ya estaba resuelta."));
        }

        var restored = false;
        if (!await ComplaintEffects.StillNeedsReviewAsync(db, complaint.TargetType, complaint.TargetId, cancellationToken))
        {
            restored = await RestoreAsync(complaint, now, cancellationToken);
        }

        if (restored)
        {
            db.ModerationActions.Add(ModerationAction.Record(
                request.StaffId,
                ModerationActionType.RestoreContent,
                complaint.TargetType,
                complaint.TargetId,
                complaint.Id,
                request.Resolution.Trim(),
                now));
        }

        Audit.Write(db, request.StaffId, "complaint.dismiss", "Complaint", complaint.Id, restored ? "restored" : "kept", now);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<bool> RestoreAsync(Complaint complaint, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (complaint.TargetType == ContentTargetType.Report)
        {
            var report = await db.Reports.FirstOrDefaultAsync(item => item.Id == complaint.TargetId, cancellationToken);
            if (report is not { Status: ReportStatus.UnderReview })
            {
                return false;
            }

            report.Restore(now);
            return true;
        }

        if (complaint.TargetType != ContentTargetType.Comment)
        {
            return false;
        }

        var comment = await db.Comments.FirstOrDefaultAsync(item => item.Id == complaint.TargetId, cancellationToken);
        if (comment is not { Status: CommentStatus.Hidden })
        {
            return false;
        }

        comment.Restore(now);
        if (comment.TargetType == ContentTargetType.Report)
        {
            var report = await db.Reports.FirstOrDefaultAsync(item => item.Id == comment.TargetId, cancellationToken);
            report?.AddComment();
        }

        return true;
    }
}

public sealed record RevealAnonymousIdentityRequest(Guid StaffId, ContentTargetType TargetType, Guid TargetId, Guid? ComplaintId, string Justification);

public sealed record RevealAnonymousIdentityResponse(Guid UserId, string UserName, string? DisplayName);

public sealed class RevealAnonymousIdentityValidator : AbstractValidator<RevealAnonymousIdentityRequest>
{
    public RevealAnonymousIdentityValidator()
    {
        RuleFor(request => request.TargetType).Must(type => type is ContentTargetType.Report or ContentTargetType.Comment);
        RuleFor(request => request.Justification).NotEmpty().MaximumLength(1000);
    }
}

public sealed class RevealAnonymousIdentityHandler(IApplicationDbContext db, IClock clock)
    : IHandler<RevealAnonymousIdentityRequest, Result<RevealAnonymousIdentityResponse>>
{
    public async Task<Result<RevealAnonymousIdentityResponse>> Handle(RevealAnonymousIdentityRequest request, CancellationToken cancellationToken)
    {
        if (!await StaffGate.HasAsync(db, request.StaffId, StaffPermissionCodes.AnonymityReveal, cancellationToken))
        {
            return Result<RevealAnonymousIdentityResponse>.Failure(InteractionErrors.RevealForbidden());
        }

        var allowed = false;
        if (request.ComplaintId is { } complaintId)
        {
            allowed = await db.Complaints.AnyAsync(
                item => item.Id == complaintId
                    && item.TargetType == request.TargetType
                    && item.TargetId == request.TargetId
                    && item.Status != ComplaintStatus.Dismissed,
                cancellationToken);
        }

        if (!allowed && request.Justification.Trim().Length >= 20)
        {
            allowed = true;
        }

        if (!allowed)
        {
            return Result<RevealAnonymousIdentityResponse>.Failure(InteractionErrors.RevealDenied());
        }

        Guid? authorId = request.TargetType == ContentTargetType.Report
            ? await db.Reports.Where(item => item.Id == request.TargetId).Select(item => (Guid?)item.AuthorId).FirstOrDefaultAsync(cancellationToken)
            : await db.Comments.Where(item => item.Id == request.TargetId).Select(item => (Guid?)item.AuthorId).FirstOrDefaultAsync(cancellationToken);
        if (authorId is null)
        {
            return Result<RevealAnonymousIdentityResponse>.Failure(InteractionErrors.ComplaintTarget());
        }

        var now = clock.UtcNow;
        db.AnonymousIdentityAccessLogs.Add(AnonymousIdentityAccessLog.Record(
            request.StaffId,
            request.TargetType,
            request.TargetId,
            request.ComplaintId,
            request.Justification.Trim(),
            now));
        Audit.Write(db, request.StaffId, "anonymity.reveal", request.TargetType.ToString(), request.TargetId, request.ComplaintId?.ToString(), now);
        try
        {
            var saved = await db.SaveChangesAsync(cancellationToken);
            if (saved < 1)
            {
                return Result<RevealAnonymousIdentityResponse>.Failure(InteractionErrors.RevealLogFailed());
            }
        }
        catch (DbUpdateException)
        {
            return Result<RevealAnonymousIdentityResponse>.Failure(InteractionErrors.RevealLogFailed());
        }

        var user = await db.Users.FirstAsync(item => item.Id == authorId.Value, cancellationToken);
        var profile = await db.UserProfiles.FirstOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
        return Result<RevealAnonymousIdentityResponse>.Success(new RevealAnonymousIdentityResponse(user.Id, user.UserName.Value, profile?.DisplayName));
    }
}
