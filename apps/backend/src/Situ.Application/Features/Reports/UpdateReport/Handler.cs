using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Profiles;
using Situ.Domain.Administration;
using Situ.Domain.Common;
using Situ.Domain.Moderation;
using Situ.Domain.News;
using Situ.Domain.Reports;

namespace Situ.Application.Features.Reports.UpdateReport
{

public sealed record UpdateReportRequest(Guid UserId, Guid ReportId, string Description);

public sealed class UpdateReportValidator : AbstractValidator<UpdateReportRequest>
{
    public UpdateReportValidator()
    {
        RuleFor(request => request.ReportId).NotEmpty();
        RuleFor(request => request.Description).NotEmpty().MaximumLength(1000);
    }
}

public sealed class UpdateReportHandler(IApplicationDbContext db, IClock clock)
    : IHandler<UpdateReportRequest, Result>
{
    public async Task<Result> Handle(UpdateReportRequest request, CancellationToken cancellationToken)
    {
        var report = await db.Reports.FirstOrDefaultAsync(item => item.Id == request.ReportId, cancellationToken);
        if (report is null || report.AuthorId != request.UserId)
        {
            return Result.Failure(report is null ? ReportErrors.NotFound() : ReportErrors.NotAuthor());
        }

        var settings = await ReportSettings.LoadAsync(db, cancellationToken);
        if (ProfileRules.ContainsBlocked(request.Description, ProfileRules.List(settings, SystemSettingKeys.ProfileBlockedTerms)))
        {
            return Result.Failure(ProfileRules.PublicationRejected());
        }

        try
        {
            report.UpdateDescription(request.Description.Trim(), clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DomainException exception)
        {
            return Result.Failure(Error.Validation(exception.Code, "El reporte ya no se puede editar."));
        }
    }
}

}

namespace Situ.Application.Features.Reports.DeleteReport
{

public sealed record DeleteReportRequest(Guid UserId, Guid ReportId);

public sealed class DeleteReportHandler(IApplicationDbContext db, IClock clock)
    : IHandler<DeleteReportRequest, Result>
{
    public async Task<Result> Handle(DeleteReportRequest request, CancellationToken cancellationToken)
    {
        var report = await db.Reports.FirstOrDefaultAsync(item => item.Id == request.ReportId, cancellationToken);
        if (report is null)
        {
            return Result.Failure(ReportErrors.NotFound());
        }

        if (report.AuthorId != request.UserId)
        {
            return Result.Failure(ReportErrors.NotAuthor());
        }

        report.Remove(clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

}

namespace Situ.Application.Features.Reports.RemoveReport
{

public sealed record RemoveReportRequest(Guid StaffId, Guid ReportId, string Reason);

public sealed class RemoveReportValidator : AbstractValidator<RemoveReportRequest>
{
    public RemoveReportValidator()
    {
        RuleFor(request => request.ReportId).NotEmpty();
        RuleFor(request => request.Reason).NotEmpty().MaximumLength(1000);
    }
}

public sealed class RemoveReportHandler(IApplicationDbContext db, IClock clock)
    : IHandler<RemoveReportRequest, Result>
{
    public async Task<Result> Handle(RemoveReportRequest request, CancellationToken cancellationToken)
    {
        if (!await StaffGate.HasAsync(db, request.StaffId, StaffPermissionCodes.ComplaintsResolve, cancellationToken))
        {
            return Result.Failure(ReportErrors.StaffForbidden());
        }

        var report = await db.Reports.FirstOrDefaultAsync(item => item.Id == request.ReportId, cancellationToken);
        if (report is null)
        {
            return Result.Failure(ReportErrors.NotFound());
        }

        var now = clock.UtcNow;
        report.Remove(now);
        db.ModerationActions.Add(ModerationAction.Record(
            request.StaffId,
            ModerationActionType.RemoveContent,
            ContentTargetType.Report,
            report.Id,
            null,
            request.Reason.Trim(),
            now));
        var reputation = await db.UserReputations.FirstOrDefaultAsync(item => item.UserId == report.AuthorId, cancellationToken);
        if (reputation is null)
        {
            reputation = UserReputation.CreateDefault(report.AuthorId, now);
            db.UserReputations.Add(reputation);
        }

        reputation.PenalizeRejectedReport(now);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
}
