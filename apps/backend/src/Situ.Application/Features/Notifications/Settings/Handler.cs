using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Reports;
using Situ.Domain.Administration;
using Situ.Domain.Notifications;

namespace Situ.Application.Features.Notifications.Settings;

public sealed record GetNotificationSettingsRequest(Guid UserId);

public sealed record UpdateNotificationSettingsRequest(
    Guid UserId,
    bool PushEnabled,
    double NearbyRadiusMeters,
    bool NotifyNearbyReports,
    bool NotifyFriendReports,
    bool NotifyFriendLives,
    bool NotifyImportantNews,
    string? QuietHoursStart,
    string? QuietHoursEnd,
    string TimeZoneId);

public sealed record NotificationSettingsResponse(
    bool PushEnabled,
    double NearbyRadiusMeters,
    bool NotifyNearbyReports,
    bool NotifyFriendReports,
    bool NotifyFriendLives,
    bool NotifyImportantNews,
    string? QuietHoursStart,
    string? QuietHoursEnd,
    string TimeZoneId);

public sealed class UpdateNotificationSettingsValidator : AbstractValidator<UpdateNotificationSettingsRequest>
{
    public UpdateNotificationSettingsValidator()
    {
        RuleFor(request => request.NearbyRadiusMeters).InclusiveBetween(100, NotificationSettings.MaxRadiusMeters);
        RuleFor(request => request.TimeZoneId).NotEmpty().MaximumLength(64);
        RuleFor(request => request.QuietHoursStart).Matches("^([01][0-9]|2[0-3]):[0-5][0-9]$").When(request => request.QuietHoursStart is not null);
        RuleFor(request => request.QuietHoursEnd).Matches("^([01][0-9]|2[0-3]):[0-5][0-9]$").When(request => request.QuietHoursEnd is not null);
    }
}

public sealed class GetNotificationSettingsHandler(IApplicationDbContext db, IClock clock)
    : IHandler<GetNotificationSettingsRequest, Result<NotificationSettingsResponse>>
{
    public async Task<Result<NotificationSettingsResponse>> Handle(GetNotificationSettingsRequest request, CancellationToken cancellationToken)
    {
        var settings = await LoadOrCreateAsync(db, clock, request.UserId, cancellationToken);
        if (settings is null)
        {
            return Result<NotificationSettingsResponse>.Failure(NotificationErrors.Unauthenticated());
        }

        return Result<NotificationSettingsResponse>.Success(Map(settings));
    }

    internal static async Task<NotificationSettings?> LoadOrCreateAsync(IApplicationDbContext db, IClock clock, Guid userId, CancellationToken cancellationToken)
    {
        var existing = await db.NotificationSettings.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var zone = await db.Cities.Where(item => item.Id == user.CityId).Select(item => item.TimeZone).FirstOrDefaultAsync(cancellationToken);
        var created = NotificationSettings.CreateDefault(userId, zone, clock.UtcNow);
        db.NotificationSettings.Add(created);
        await db.SaveChangesAsync(cancellationToken);
        return created;
    }

    internal static NotificationSettingsResponse Map(NotificationSettings settings) => new(
        settings.PushEnabled,
        settings.NearbyRadiusMeters,
        settings.NotifyNearbyReports,
        settings.NotifyFriendReports,
        settings.NotifyFriendLives,
        settings.NotifyImportantNews,
        settings.QuietHoursStart?.ToString("HH:mm"),
        settings.QuietHoursEnd?.ToString("HH:mm"),
        settings.TimeZoneId);
}

public sealed class UpdateNotificationSettingsHandler(IApplicationDbContext db, IClock clock)
    : IHandler<UpdateNotificationSettingsRequest, Result<NotificationSettingsResponse>>
{
    public async Task<Result<NotificationSettingsResponse>> Handle(UpdateNotificationSettingsRequest request, CancellationToken cancellationToken)
    {
        if ((request.QuietHoursStart is null) != (request.QuietHoursEnd is null))
        {
            return Result<NotificationSettingsResponse>.Failure(NotificationErrors.QuietHours());
        }

        if (!KnownZone(request.TimeZoneId))
        {
            return Result<NotificationSettingsResponse>.Failure(NotificationErrors.TimeZone());
        }

        var settings = await GetNotificationSettingsHandler.LoadOrCreateAsync(db, clock, request.UserId, cancellationToken);
        if (settings is null)
        {
            return Result<NotificationSettingsResponse>.Failure(NotificationErrors.Unauthenticated());
        }

        var now = clock.UtcNow;
        try
        {
            settings.Update(
                request.PushEnabled,
                settings.EmailEnabled,
                request.NearbyRadiusMeters,
                request.NotifyNearbyReports,
                request.NotifyFriendReports,
                request.NotifyFriendLives,
                request.NotifyImportantNews,
                request.QuietHoursStart is null ? null : TimeOnly.Parse(request.QuietHoursStart),
                request.QuietHoursEnd is null ? null : TimeOnly.Parse(request.QuietHoursEnd),
                request.TimeZoneId,
                now);
        }
        catch (Situ.Domain.Common.DomainException)
        {
            return Result<NotificationSettingsResponse>.Failure(NotificationErrors.QuietHours());
        }

        if (!request.NotifyNearbyReports)
        {
            await DeviceAlerts.ForgetUserAsync(db, request.UserId, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result<NotificationSettingsResponse>.Success(GetNotificationSettingsHandler.Map(settings));
    }

    private static bool KnownZone(string id)
    {
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}

public sealed record ListInterestCitiesRequest(Guid UserId);
public sealed record AddInterestCityRequest(Guid UserId, Guid CityId);
public sealed record RemoveInterestCityRequest(Guid UserId, Guid CityId);
public sealed record InterestCityItem(Guid CityId, string Name);
public sealed record InterestCitiesResponse(IReadOnlyList<InterestCityItem> Cities, int Maximum);

public sealed class ListInterestCitiesHandler(IApplicationDbContext db, IClock clock)
    : IHandler<ListInterestCitiesRequest, Result<InterestCitiesResponse>>
{
    public async Task<Result<InterestCitiesResponse>> Handle(ListInterestCitiesRequest request, CancellationToken cancellationToken)
    {
        var maximum = await MaximumAsync(db, cancellationToken);
        var rows = await db.UserInterestCities.Where(item => item.UserId == request.UserId).ToListAsync(cancellationToken);
        var cityIds = rows.Select(item => item.CityId).ToArray();
        var cities = await db.Cities.Where(item => cityIds.Contains(item.Id)).ToListAsync(cancellationToken);
        var items = rows.Select(row => new InterestCityItem(row.CityId, cities.FirstOrDefault(item => item.Id == row.CityId)?.Name ?? "Ciudad")).ToList();
        _ = clock;
        return Result<InterestCitiesResponse>.Success(new InterestCitiesResponse(items, maximum));
    }

    internal static async Task<int> MaximumAsync(IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var settings = await ReportSettings.LoadAsync(db, cancellationToken);
        return Math.Clamp(ReportSettings.Int(settings, SystemSettingKeys.NotificationMaxInterestCities, 3), 0, 50);
    }
}

public sealed class AddInterestCityHandler(IApplicationDbContext db, IClock clock)
    : IHandler<AddInterestCityRequest, Result<InterestCitiesResponse>>
{
    public async Task<Result<InterestCitiesResponse>> Handle(AddInterestCityRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        if (user is null)
        {
            return Result<InterestCitiesResponse>.Failure(NotificationErrors.Unauthenticated());
        }

        if (user.CityId == request.CityId)
        {
            return Result<InterestCitiesResponse>.Failure(NotificationErrors.HomeCity());
        }

        var city = await db.Cities.FirstOrDefaultAsync(item => item.Id == request.CityId && item.IsEnabled, cancellationToken);
        if (city is null)
        {
            return Result<InterestCitiesResponse>.Failure(NotificationErrors.CityNotFound());
        }

        var rows = await db.UserInterestCities.Where(item => item.UserId == request.UserId).ToListAsync(cancellationToken);
        if (rows.Any(item => item.CityId == request.CityId))
        {
            return Result<InterestCitiesResponse>.Failure(NotificationErrors.CityExists());
        }

        var maximum = await ListInterestCitiesHandler.MaximumAsync(db, cancellationToken);
        if (rows.Count >= maximum)
        {
            return Result<InterestCitiesResponse>.Failure(NotificationErrors.CityLimit());
        }

        db.UserInterestCities.Add(UserInterestCity.Create(request.UserId, request.CityId));
        _ = clock;
        await db.SaveChangesAsync(cancellationToken);
        return await new ListInterestCitiesHandler(db, clock).Handle(new ListInterestCitiesRequest(request.UserId), cancellationToken);
    }
}

public sealed class RemoveInterestCityHandler(IApplicationDbContext db, IClock clock)
    : IHandler<RemoveInterestCityRequest, Result<InterestCitiesResponse>>
{
    public async Task<Result<InterestCitiesResponse>> Handle(RemoveInterestCityRequest request, CancellationToken cancellationToken)
    {
        var row = await db.UserInterestCities.FirstOrDefaultAsync(item => item.UserId == request.UserId && item.CityId == request.CityId, cancellationToken);
        if (row is null)
        {
            return Result<InterestCitiesResponse>.Failure(NotificationErrors.CityNotFound());
        }

        db.UserInterestCities.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
        return await new ListInterestCitiesHandler(db, clock).Handle(new ListInterestCitiesRequest(request.UserId), cancellationToken);
    }
}

public sealed record GetCategoryInterestsRequest(Guid UserId);
public sealed record UpdateCategoryInterestsRequest(Guid UserId, IReadOnlyList<Guid> MutedCategoryIds);
public sealed record CategoryInterestsResponse(IReadOnlyList<Guid> MutedCategoryIds);

public sealed class GetCategoryInterestsHandler(IApplicationDbContext db)
    : IHandler<GetCategoryInterestsRequest, Result<CategoryInterestsResponse>>
{
    public async Task<Result<CategoryInterestsResponse>> Handle(GetCategoryInterestsRequest request, CancellationToken cancellationToken)
    {
        var muted = await db.UserCategoryInterests
            .Where(item => item.UserId == request.UserId && item.IsMuted)
            .Select(item => item.CategoryId)
            .ToListAsync(cancellationToken);
        return Result<CategoryInterestsResponse>.Success(new CategoryInterestsResponse(muted));
    }
}

public sealed class UpdateCategoryInterestsHandler(IApplicationDbContext db)
    : IHandler<UpdateCategoryInterestsRequest, Result<CategoryInterestsResponse>>
{
    public async Task<Result<CategoryInterestsResponse>> Handle(UpdateCategoryInterestsRequest request, CancellationToken cancellationToken)
    {
        var ids = request.MutedCategoryIds.Distinct().ToArray();
        var found = await db.ReportCategories.CountAsync(item => ids.Contains(item.Id), cancellationToken);
        if (found != ids.Length)
        {
            return Result<CategoryInterestsResponse>.Failure(NotificationErrors.CategoryNotFound());
        }

        var current = await db.UserCategoryInterests.Where(item => item.UserId == request.UserId).ToListAsync(cancellationToken);
        db.UserCategoryInterests.RemoveRange(current);
        foreach (var id in ids)
        {
            db.UserCategoryInterests.Add(UserCategoryInterest.Create(request.UserId, id, true));
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result<CategoryInterestsResponse>.Success(new CategoryInterestsResponse(ids));
    }
}
