using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Domain.Administration;
using Situ.Domain.Common;

namespace Situ.Application.Features.Administration.Settings;

public sealed record ListSettingsRequest(Guid ActorId);

public sealed record SettingItemView(string Key, string Value, string? Description, bool ReadOnly);

public sealed record SettingGroupView(string Name, IReadOnlyList<SettingItemView> Items);

public sealed record ListSettingsResponse(IReadOnlyList<SettingGroupView> Groups);

public sealed class ListSettingsHandler(IApplicationDbContext db)
    : IHandler<ListSettingsRequest, Result<ListSettingsResponse>>
{
    public async Task<Result<ListSettingsResponse>> Handle(ListSettingsRequest request, CancellationToken cancellationToken)
    {
        if (await AdminSupport.Allow(db, request.ActorId, StaffPermissionCodes.SettingsManage, cancellationToken) is { } denied)
        {
            return Result<ListSettingsResponse>.Failure(denied);
        }

        var stored = await db.SystemSettings.AsNoTracking().ToListAsync(cancellationToken);
        var byKey = stored.Where(item => SettingsRules.IsVisible(item.Key)).ToDictionary(item => item.Key, item => item);
        var items = SystemSettingKeys.Defaults.Keys
            .Where(SettingsRules.IsVisible)
            .Select(key =>
            {
                byKey.TryGetValue(key, out var row);
                return new SettingItemView(
                    key,
                    row?.Value ?? SystemSettingKeys.Defaults[key],
                    row?.Description,
                    key == SystemSettingKeys.ReportCatalogVersion);
            })
            .GroupBy(item => item.Key.Split('.')[0], StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new SettingGroupView(group.Key, group.OrderBy(item => item.Key, StringComparer.Ordinal).ToList()))
            .ToList();
        return Result<ListSettingsResponse>.Success(new ListSettingsResponse(items));
    }
}

public sealed record UpdateSettingRequest(Guid ActorId, string? IpAddress, string Key, string Value);

public sealed class UpdateSettingValidator : AbstractValidator<UpdateSettingRequest>
{
    public UpdateSettingValidator()
    {
        RuleFor(request => request.Key).NotEmpty().MaximumLength(100);
        RuleFor(request => request.Value).NotNull().MaximumLength(1000);
    }
}

public sealed class UpdateSettingHandler(IApplicationDbContext db, IClock clock)
    : IHandler<UpdateSettingRequest, Result>
{
    public async Task<Result> Handle(UpdateSettingRequest request, CancellationToken cancellationToken)
    {
        if (await AdminSupport.Allow(db, request.ActorId, StaffPermissionCodes.SettingsManage, cancellationToken) is { } denied)
        {
            return Result.Failure(denied);
        }

        var validated = SettingsRules.Validate(request.Key, request.Value);
        if (validated.IsFailure)
        {
            return Result.Failure(validated.Error!);
        }

        var now = clock.UtcNow;
        var setting = await db.SystemSettings.FirstOrDefaultAsync(item => item.Key == request.Key, cancellationToken);
        var before = setting?.Value ?? SystemSettingKeys.Defaults[request.Key];
        if (setting is null)
        {
            setting = SystemSetting.Create(request.Key, validated.Value!, null, now);
            db.SystemSettings.Add(setting);
        }
        else
        {
            setting.ChangeValue(validated.Value!, now);
        }

        AdminSupport.Record(
            db,
            request.ActorId,
            "settings.update",
            nameof(SystemSetting),
            request.Key,
            AdminSupport.Json(new { before, after = validated.Value }),
            request.IpAddress,
            now);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record ChangeCountryMinimumAgeRequest(Guid ActorId, string? IpAddress, Guid CountryId, int MinimumAge);

public sealed class ChangeCountryMinimumAgeValidator : AbstractValidator<ChangeCountryMinimumAgeRequest>
{
    public ChangeCountryMinimumAgeValidator()
    {
        RuleFor(request => request.MinimumAge).InclusiveBetween(13, 18);
    }
}

public sealed class ChangeCountryMinimumAgeHandler(IApplicationDbContext db, IClock clock)
    : IHandler<ChangeCountryMinimumAgeRequest, Result>
{
    public async Task<Result> Handle(ChangeCountryMinimumAgeRequest request, CancellationToken cancellationToken)
    {
        if (await AdminSupport.Allow(db, request.ActorId, StaffPermissionCodes.SettingsManage, cancellationToken) is { } denied)
        {
            return Result.Failure(denied);
        }

        var country = await db.Countries.FirstOrDefaultAsync(item => item.Id == request.CountryId, cancellationToken);
        if (country is null)
        {
            return Result.Failure(Error.NotFound("country.not_found", "No encontramos el país."));
        }

        var before = country.MinimumAge;
        try
        {
            country.ChangeMinimumAge(request.MinimumAge);
        }
        catch (DomainException exception)
        {
            return Result.Failure(Error.Validation(exception.Code, "La edad mínima tiene que estar entre 13 y 18."));
        }

        AdminSupport.Record(db, request.ActorId, "country.minimum_age", "Country", country.Id.ToString(), AdminSupport.Json(new { before, after = country.MinimumAge }), request.IpAddress, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
