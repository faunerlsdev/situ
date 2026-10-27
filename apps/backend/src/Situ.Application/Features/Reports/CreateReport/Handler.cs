using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Profiles;
using Situ.Domain.Administration;
using Situ.Domain.Common;
using Situ.Domain.Guardianship;
using Situ.Domain.Identity;
using Situ.Domain.Monetization;
using Situ.Domain.Reports;

namespace Situ.Application.Features.Reports.CreateReport;

public sealed record CreateReportRequest(
    Guid UserId,
    Guid CategoryId,
    string Description,
    double Latitude,
    double Longitude,
    ContentVisibility Visibility,
    bool IsAnonymous,
    IReadOnlyList<Guid> MediaIds);

public sealed record CreateReportResponse(ReportCardView Report);

public sealed class CreateReportValidator : AbstractValidator<CreateReportRequest>
{
    public CreateReportValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.CategoryId).NotEmpty();
        RuleFor(request => request.Description).NotEmpty().MaximumLength(1000);
        RuleFor(request => request.Latitude).InclusiveBetween(-90, 90);
        RuleFor(request => request.Longitude).InclusiveBetween(-180, 180);
        RuleFor(request => request.MediaIds).NotNull();
    }
}

public sealed class CreateReportHandler(IApplicationDbContext db, IClock clock, IMediaUrlSigner urls)
    : IHandler<CreateReportRequest, Result<CreateReportResponse>>
{
    public async Task<Result<CreateReportResponse>> Handle(CreateReportRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        if (user is null)
        {
            return Result<CreateReportResponse>.Failure(ReportErrors.NotFound());
        }

        if (user.Status == UserStatus.PendingGuardianConsent)
        {
            return Result<CreateReportResponse>.Failure(ReportErrors.PendingGuardian());
        }

        if (user.EmailConfirmedAt is null)
        {
            return Result<CreateReportResponse>.Failure(ReportErrors.EmailUnconfirmed());
        }

        if (!user.CanPublish)
        {
            return Result<CreateReportResponse>.Failure(ReportErrors.CannotPublish());
        }

        if (!MinorProtectionPolicy.CanPublishReport(user.AgeOn(now), request.Visibility, request.IsAnonymous))
        {
            return Result<CreateReportResponse>.Failure(ReportErrors.MinorPublicIdentity());
        }

        var category = await db.ReportCategories.FirstOrDefaultAsync(item => item.Id == request.CategoryId && item.IsActive, cancellationToken);
        if (category is null)
        {
            return Result<CreateReportResponse>.Failure(ReportErrors.CategoryUnavailable());
        }

        if (category.IsAdultOnly && !MinorProtectionPolicy.CanUseAdultOnlyCategories(user.AgeOn(now)))
        {
            return Result<CreateReportResponse>.Failure(ReportErrors.AdultOnly());
        }

        var nearest = await NearestCityAsync(db, request.Latitude, request.Longitude, cancellationToken);
        var cityId = nearest?.Id ?? user.CityId;
        var countryId = nearest?.CountryId ?? user.CountryId;
        var cityOverride = await db.ReportCategoryOverrides.FirstOrDefaultAsync(item => item.CategoryId == category.Id && item.CityId == cityId, cancellationToken);
        var countryOverride = await db.ReportCategoryOverrides.FirstOrDefaultAsync(
            item => item.CategoryId == category.Id && item.CountryId == countryId && item.CityId == null,
            cancellationToken);
        var chosen = cityOverride ?? countryOverride;
        if (chosen is { IsEnabled: false })
        {
            return Result<CreateReportResponse>.Failure(ReportErrors.CategoryUnavailable());
        }

        var media = await db.ReportMedias
            .Where(item => request.MediaIds.Contains(item.Id) && item.OwnerUserId == user.Id && item.ReportId == null)
            .ToListAsync(cancellationToken);
        if (media.Count != request.MediaIds.Count || media.Any(item => item.ProcessingStatus != MediaProcessingStatus.Ready))
        {
            return Result<CreateReportResponse>.Failure(ReportErrors.MediaNotFound());
        }

        if (!MeetsMediaRequirement(category.MediaRequirement, media))
        {
            return Result<CreateReportResponse>.Failure(ReportErrors.MediaRequired());
        }

        var settings = await ReportSettings.LoadAsync(db, cancellationToken);
        var images = media.Count(item => item.Type == MediaType.Image);
        var videos = media.Count(item => item.Type == MediaType.Video);
        if (images > ReportSettings.Int(settings, SystemSettingKeys.ReportMaxImages, 5) || videos > ReportSettings.Int(settings, SystemSettingKeys.ReportMaxVideos, 1))
        {
            return Result<CreateReportResponse>.Failure(Error.Validation("report.too_many_files", "El reporte supera el máximo de fotos o videos."));
        }

        var plan = await ResolvePlanAsync(db, user.Id, now, cancellationToken);
        var dayStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var tomorrow = dayStart.AddDays(1);
        var todayCount = await db.Reports.CountAsync(item => item.AuthorId == user.Id && item.CreatedAt >= dayStart, cancellationToken);
        if (plan.MaxReportsPerDay is { } maxReports && todayCount >= maxReports)
        {
            return Result<CreateReportResponse>.Failure(ReportErrors.DailyLimit(tomorrow));
        }

        if (request.IsAnonymous && plan.MaxAnonymousPostsPerDay is { } maxAnonymous)
        {
            var anonymousCount = await db.Reports.CountAsync(
                item => item.AuthorId == user.Id && item.IsAnonymous && item.CreatedAt >= dayStart,
                cancellationToken);
            if (anonymousCount >= maxAnonymous)
            {
                return Result<CreateReportResponse>.Failure(ReportErrors.AnonymousDailyLimit(tomorrow));
            }
        }

        if (ProfileRules.ContainsBlocked(request.Description, ProfileRules.List(settings, SystemSettingKeys.ProfileBlockedTerms)))
        {
            return Result<CreateReportResponse>.Failure(ProfileRules.PublicationRejected());
        }

        GeoPoint location;
        try
        {
            location = new GeoPoint(request.Latitude, request.Longitude);
        }
        catch (DomainException exception)
        {
            return Result<CreateReportResponse>.Failure(ReportErrors.FromDomain(exception));
        }

        Report report;
        try
        {
            report = Report.Create(
                user.Id,
                category.Id,
                cityId,
                location,
                request.Description.Trim(),
                request.IsAnonymous,
                user.IsMinor(now),
                category.EffectiveLifetime(chosen?.TtlMinutes),
                now,
                visibility: request.Visibility);
        }
        catch (DomainException exception)
        {
            return Result<CreateReportResponse>.Failure(ReportErrors.FromDomain(exception));
        }

        db.Reports.Add(report);
        for (var index = 0; index < media.Count; index++)
        {
            media[index].Attach(report.Id, index);
        }

        await db.SaveChangesAsync(cancellationToken);
        var profile = request.IsAnonymous
            ? null
            : await db.UserProfiles.FirstOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
        var signed = urls.Sign(media.FirstOrDefault()?.Id ?? Guid.Empty, now.AddMinutes(15));
        var preview = media.FirstOrDefault() is { } first
            ? ReportPresentation.ReadUrl(first.Id, signed.ExpiresUnix, signed.Signature)
            : null;
        var card = new ReportCardView(
            report.Id,
            category.Id,
            category.Code,
            report.Description,
            location.Latitude,
            location.Longitude,
            report.Visibility.ToString(),
            report.IsAnonymous,
            report.HideAuthorOnMap,
            report.Status.ToString(),
            report.CreatedAt,
            report.ExpiresAt,
            0,
            0,
            0,
            0,
            ReportPresentation.Author(report.IsAnonymous, user, profile, ReportSettings.DisplayName(settings)),
            preview,
            [],
            null);
        return Result<CreateReportResponse>.Success(new CreateReportResponse(card));
    }

    internal static bool MeetsMediaRequirement(MediaRequirement requirement, IReadOnlyList<ReportMedia> media) => requirement switch
    {
        MediaRequirement.None => true,
        MediaRequirement.PhotoRequired => media.Any(item => item.Type == MediaType.Image),
        MediaRequirement.VideoRequired => media.Any(item => item.Type == MediaType.Video),
        MediaRequirement.PhotoOrVideoRequired => media.Count > 0,
        _ => true,
    };

    private static async Task<CityMatch?> NearestCityAsync(
        IApplicationDbContext db,
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        var point = new GeoPoint(latitude, longitude);
        var cities = await db.Cities.Where(city => city.IsEnabled).Select(city => new { city.Id, city.CountryId, city.Center }).ToListAsync(cancellationToken);
        var nearest = cities
            .Select(city => new { city.Id, city.CountryId, Distance = city.Center.DistanceInMetersTo(point) })
            .OrderBy(city => city.Distance)
            .FirstOrDefault();
        return nearest is { Distance: <= 50_000 } match ? new CityMatch(match.Id, match.CountryId) : null;
    }

    private static async Task<SubscriptionPlan> ResolvePlanAsync(
        IApplicationDbContext db,
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var active = await (
            from subscription in db.Subscriptions
            join plan in db.SubscriptionPlans on subscription.PlanId equals plan.Id
            where subscription.UserId == userId && subscription.CurrentPeriodEnd > now
                && (subscription.Status == SubscriptionStatus.Active || subscription.Status == SubscriptionStatus.PastDue)
            select plan).FirstOrDefaultAsync(cancellationToken);
        return active ?? await db.SubscriptionPlans.FirstAsync(plan => plan.Code == "free", cancellationToken);
    }

    private sealed record CityMatch(Guid Id, Guid CountryId);
}
