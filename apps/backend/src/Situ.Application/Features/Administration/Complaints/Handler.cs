using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Domain.Administration;
using Situ.Domain.Common;
using Situ.Domain.Moderation;
using Situ.Domain.Reports;

namespace Situ.Application.Features.Administration.Complaints;

public sealed record GetComplaintContextRequest(Guid ActorId, Guid ComplaintId);

public sealed record ComplaintMediaView(Guid Id, string Type, string Status);

public sealed record ComplaintAuthorView(Guid Id, string UserName);

public sealed record ComplaintSanctionView(Guid Id, string Action, string Reason, DateTimeOffset CreatedAt);

public sealed record RelatedComplaintView(Guid Id, string Reason, string Status, bool IsPriority, DateTimeOffset CreatedAt);

public sealed record ComplaintTargetView(
    string Type,
    Guid Id,
    string? Text,
    string? Status,
    int? StillThereCount,
    int? GoneNowCount,
    int? CommentCount,
    IReadOnlyList<ComplaintMediaView> Media,
    bool AuthorHidden,
    ComplaintAuthorView? Author);

public sealed record GetComplaintContextResponse(
    Guid Id,
    string Reason,
    string Status,
    bool IsPriority,
    string? Details,
    DateTimeOffset CreatedAt,
    string? ReporterUserName,
    ComplaintTargetView Target,
    IReadOnlyList<RelatedComplaintView> OtherComplaints,
    IReadOnlyList<ComplaintSanctionView> Sanctions);

public sealed class GetComplaintContextHandler(IApplicationDbContext db)
    : IHandler<GetComplaintContextRequest, Result<GetComplaintContextResponse>>
{
    public async Task<Result<GetComplaintContextResponse>> Handle(GetComplaintContextRequest request, CancellationToken cancellationToken)
    {
        if (await AdminSupport.Allow(db, request.ActorId, StaffPermissionCodes.ComplaintsResolve, cancellationToken) is { } denied)
        {
            return Result<GetComplaintContextResponse>.Failure(denied);
        }

        var complaint = await db.Complaints.FirstOrDefaultAsync(item => item.Id == request.ComplaintId, cancellationToken);
        if (complaint is null)
        {
            return Result<GetComplaintContextResponse>.Failure(Error.NotFound("complaint.not_found", "No encontramos la denuncia."));
        }

        var reporter = await db.Users.Where(item => item.Id == complaint.ReporterId).Select(item => item.UserName).FirstOrDefaultAsync(cancellationToken);
        var target = await TargetAsync(complaint, cancellationToken);
        if (target is null)
        {
            return Result<GetComplaintContextResponse>.Failure(Error.NotFound("complaint.target_missing", "El contenido denunciado ya no está."));
        }

        var others = await db.Complaints
            .Where(item => item.Id != complaint.Id && item.TargetType == complaint.TargetType && item.TargetId == complaint.TargetId)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new RelatedComplaintView(item.Id, item.Reason.ToString(), item.Status.ToString(), item.IsPriority, item.CreatedAt))
            .ToListAsync(cancellationToken);
        var sanctions = target.Author is null
            ? []
            : await db.ModerationActions
                .Where(item => item.TargetType == ContentTargetType.User && item.TargetId == target.Author.Id)
                .OrderByDescending(item => item.CreatedAt)
                .Select(item => new ComplaintSanctionView(item.Id, item.Action.ToString(), item.Reason, item.CreatedAt))
                .ToListAsync(cancellationToken);

        return Result<GetComplaintContextResponse>.Success(new GetComplaintContextResponse(
            complaint.Id,
            complaint.Reason.ToString(),
            complaint.Status.ToString(),
            complaint.IsPriority,
            complaint.Details,
            complaint.CreatedAt,
            reporter?.Value,
            target,
            others,
            sanctions));
    }

    private async Task<ComplaintTargetView?> TargetAsync(Complaint complaint, CancellationToken cancellationToken)
    {
        if (complaint.TargetType == ContentTargetType.Report)
        {
            var report = await db.Reports.FirstOrDefaultAsync(item => item.Id == complaint.TargetId, cancellationToken);
            if (report is null)
            {
                return null;
            }

            var media = await db.ReportMedias
                .Where(item => item.ReportId == report.Id)
                .Select(item => new ComplaintMediaView(item.Id, item.Type.ToString(), item.ProcessingStatus.ToString()))
                .ToListAsync(cancellationToken);
            return new ComplaintTargetView(
                "Report",
                report.Id,
                report.Description,
                report.Status.ToString(),
                report.StillThereCount,
                report.GoneNowCount,
                report.CommentCount,
                media,
                report.IsAnonymous,
                report.IsAnonymous ? null : await AuthorAsync(report.AuthorId, cancellationToken));
        }

        if (complaint.TargetType == ContentTargetType.Comment)
        {
            var comment = await db.Comments.FirstOrDefaultAsync(item => item.Id == complaint.TargetId, cancellationToken);
            if (comment is null)
            {
                return null;
            }

            return new ComplaintTargetView(
                "Comment",
                comment.Id,
                comment.Text,
                comment.Status.ToString(),
                null,
                null,
                null,
                [],
                comment.IsAnonymous,
                comment.IsAnonymous ? null : await AuthorAsync(comment.AuthorId, cancellationToken));
        }

        if (complaint.TargetType == ContentTargetType.User)
        {
            var author = await AuthorAsync(complaint.TargetId, cancellationToken);
            return author is null
                ? null
                : new ComplaintTargetView("User", author.Id, null, null, null, null, null, [], false, author);
        }

        return new ComplaintTargetView(complaint.TargetType.ToString(), complaint.TargetId, null, null, null, null, null, [], true, null);
    }

    private async Task<ComplaintAuthorView?> AuthorAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.Where(item => item.Id == userId).Select(item => new { item.Id, item.UserName }).FirstOrDefaultAsync(cancellationToken);
        return user is null ? null : new ComplaintAuthorView(user.Id, user.UserName.Value);
    }
}
