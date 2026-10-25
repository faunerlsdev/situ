using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Domain.Administration;
using Situ.Domain.Common;
using Situ.Domain.News;
using Situ.Domain.Reports;

namespace Situ.Application.Features.Reports.ConfirmReport;

public sealed record ConfirmReportRequest(
    Guid UserId,
    Guid ReportId,
    ConfirmationType Type,
    double Latitude,
    double Longitude);

public sealed record ConfirmReportResponse(string Type, int StillThereCount, int GoneNowCount, string Status, DateTimeOffset ExpiresAt);

public sealed class ConfirmReportValidator : AbstractValidator<ConfirmReportRequest>
{
    public ConfirmReportValidator()
    {
        RuleFor(request => request.ReportId).NotEmpty();
        RuleFor(request => request.Latitude).InclusiveBetween(-90, 90);
        RuleFor(request => request.Longitude).InclusiveBetween(-180, 180);
        RuleFor(request => request.Type).IsInEnum();
    }
}

public sealed class ConfirmReportHandler(IApplicationDbContext db, IClock clock)
    : IHandler<ConfirmReportRequest, Result<ConfirmReportResponse>>
{
    public async Task<Result<ConfirmReportResponse>> Handle(ConfirmReportRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var report = await db.Reports.FirstOrDefaultAsync(item => item.Id == request.ReportId, cancellationToken);
        if (report is null || !report.IsVisible(now))
        {
            return Result<ConfirmReportResponse>.Failure(ReportErrors.NotFound());
        }

        var friends = await ReportAccess.FriendIdsAsync(db, request.UserId, cancellationToken);
        var blocks = await ReportAccess.BlocksAsync(db, request.UserId, cancellationToken);
        if (!ReportAccess.CanSee(report, request.UserId, friends, blocks))
        {
            return Result<ConfirmReportResponse>.Failure(ReportErrors.NotFound());
        }

        if (report.AuthorId == request.UserId)
        {
            return Result<ConfirmReportResponse>.Failure(ReportErrors.ConfirmationOwn());
        }

        GeoPoint voter;
        try
        {
            voter = new GeoPoint(request.Latitude, request.Longitude);
        }
        catch (DomainException exception)
        {
            return Result<ConfirmReportResponse>.Failure(ReportErrors.FromDomain(exception));
        }

        var settings = await ReportSettings.LoadAsync(db, cancellationToken);
        var radius = ReportSettings.Int(settings, SystemSettingKeys.ReportConfirmationRadiusMeters, 2000);
        if (!report.Location.IsWithin(voter, radius))
        {
            return Result<ConfirmReportResponse>.Failure(ReportErrors.ConfirmationTooFar());
        }

        var category = await db.ReportCategories.FirstAsync(item => item.Id == report.CategoryId, cancellationToken);
        var percent = ReportSettings.Int(settings, SystemSettingKeys.ReportConfirmationExtensionPercent, 25);
        var minimum = TimeSpan.FromMinutes(ReportSettings.Int(settings, SystemSettingKeys.ReportConfirmationMinExtensionMinutes, 10));
        var extension = TimeSpan.FromTicks(category.EffectiveLifetime().Ticks * percent / 100);
        if (extension < minimum)
        {
            extension = minimum;
        }

        var existing = await db.ReportConfirmations.FirstOrDefaultAsync(
            item => item.ReportId == report.Id && item.UserId == request.UserId,
            cancellationToken);
        var previous = existing?.Type;
        var reward = request.Type == ConfirmationType.StillThere && previous is null;
        if (existing is null)
        {
            existing = ReportConfirmation.Create(report.Id, request.UserId, request.Type, voter, now);
            db.ReportConfirmations.Add(existing);
        }
        else
        {
            existing.Change(request.Type, voter, now);
        }

        report.ReplaceConfirmation(previous, request.Type, extension, now);
        if (report.ShouldExpireByVotes())
        {
            report.Expire(now);
        }

        if (reward)
        {
            var reputation = await db.UserReputations.FirstOrDefaultAsync(item => item.UserId == report.AuthorId, cancellationToken);
            if (reputation is null)
            {
                reputation = UserReputation.CreateDefault(report.AuthorId, now);
                db.UserReputations.Add(reputation);
            }

            reputation.RewardConfirmedReport(now);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result<ConfirmReportResponse>.Success(new ConfirmReportResponse(
            request.Type.ToString(),
            report.StillThereCount,
            report.GoneNowCount,
            report.Status.ToString(),
            report.ExpiresAt));
    }
}
