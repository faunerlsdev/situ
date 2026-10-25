using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Reports;
using Situ.Domain.Administration;
using Situ.Domain.Guardianship;
using Situ.Domain.Notifications;

namespace Situ.Application.Features.Notifications.Devices;

public sealed record SetPushTokenRequest(Guid UserId, Guid? DeviceId, string? Token);
public sealed record ClearPushTokenRequest(Guid UserId, Guid? DeviceId);
public sealed record SetAlertLocationRequest(Guid UserId, Guid? DeviceId, double Latitude, double Longitude, double? AccuracyMeters);
public sealed record ClearAlertLocationRequest(Guid UserId, Guid? DeviceId);

public sealed class SetPushTokenValidator : AbstractValidator<SetPushTokenRequest>
{
    public SetPushTokenValidator()
    {
        RuleFor(request => request.Token).NotEmpty().MaximumLength(512);
    }
}

public sealed class SetAlertLocationValidator : AbstractValidator<SetAlertLocationRequest>
{
    public SetAlertLocationValidator()
    {
        RuleFor(request => request.Latitude).InclusiveBetween(-90, 90);
        RuleFor(request => request.Longitude).InclusiveBetween(-180, 180);
        RuleFor(request => request.AccuracyMeters).InclusiveBetween(0, 10_000).When(request => request.AccuracyMeters is not null);
    }
}

public sealed class SetPushTokenHandler(IApplicationDbContext db, IClock clock)
    : IHandler<SetPushTokenRequest, Result>
{
    public async Task<Result> Handle(SetPushTokenRequest request, CancellationToken cancellationToken)
    {
        var device = await CurrentDevice.FindAsync(db, request.UserId, request.DeviceId, cancellationToken);
        if (device.Error is { } error)
        {
            return Result.Failure(error);
        }

        device.Device!.UpdatePushToken(request.Token);
        device.Device.Touch(clock.UtcNow, null);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class ClearPushTokenHandler(IApplicationDbContext db)
    : IHandler<ClearPushTokenRequest, Result>
{
    public async Task<Result> Handle(ClearPushTokenRequest request, CancellationToken cancellationToken)
    {
        var device = await CurrentDevice.FindAsync(db, request.UserId, request.DeviceId, cancellationToken);
        if (device.Error is { } error)
        {
            return Result.Failure(error);
        }

        device.Device!.UpdatePushToken(null);
        if (device.Device is not null)
        {
            await DeviceAlerts.ForgetDevicesAsync(db, [device.Device.Id], cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class SetAlertLocationHandler(IApplicationDbContext db, IClock clock)
    : IHandler<SetAlertLocationRequest, Result>
{
    public async Task<Result> Handle(SetAlertLocationRequest request, CancellationToken cancellationToken)
    {
        var device = await CurrentDevice.FindAsync(db, request.UserId, request.DeviceId, cancellationToken);
        if (device.Error is { } error)
        {
            return Result.Failure(error);
        }

        var user = await db.Users.FirstAsync(item => item.Id == request.UserId, cancellationToken);
        var preference = await db.NotificationSettings.FirstOrDefaultAsync(item => item.UserId == request.UserId, cancellationToken);
        if (preference is { NotifyNearbyReports: false })
        {
            return Result.Failure(NotificationErrors.ProximityOff());
        }

        if (user.IsMinor(clock.UtcNow))
        {
            var allowed = await db.GuardianConsents.AnyAsync(
                item => item.MinorUserId == user.Id && item.Status == GuardianConsentStatus.Authorized && item.LocationSharingAuthorized,
                cancellationToken);
            if (!MinorProtectionPolicy.CanShareProximityLocation(user.AgeOn(clock.UtcNow), allowed))
            {
                return Result.Failure(NotificationErrors.ProximityMinor());
            }
        }

        var now = clock.UtcNow;
        var stored = await ReportSettings.LoadAsync(db, cancellationToken);
        var minutes = Math.Clamp(ReportSettings.Int(stored, SystemSettingKeys.NotificationAlertLocationMinutes, 30), 1, 24 * 60);
        var lifetime = TimeSpan.FromMinutes(minutes);
        var current = await db.DeviceAlertLocations.FirstOrDefaultAsync(item => item.DeviceId == device.Device!.Id, cancellationToken);
        if (current is null)
        {
            db.DeviceAlertLocations.Add(DeviceAlertLocation.Record(user.Id, device.Device!.Id, request.Latitude, request.Longitude, request.AccuracyMeters, now, lifetime));
        }
        else if (!current.CanRefresh(now))
        {
            return Result.Failure(NotificationErrors.LocationLimited(current.ReportedAt.Add(DeviceAlertLocation.MinimumInterval)));
        }
        else
        {
            current.Refresh(request.Latitude, request.Longitude, request.AccuracyMeters, now, lifetime);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class ClearAlertLocationHandler(IApplicationDbContext db)
    : IHandler<ClearAlertLocationRequest, Result>
{
    public async Task<Result> Handle(ClearAlertLocationRequest request, CancellationToken cancellationToken)
    {
        var device = await CurrentDevice.FindAsync(db, request.UserId, request.DeviceId, cancellationToken);
        if (device.Error is { } error)
        {
            return Result.Failure(error);
        }

        await DeviceAlerts.ForgetDevicesAsync(db, [device.Device!.Id], cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal static class CurrentDevice
{
    public static async Task<(Situ.Domain.Identity.UserDevice? Device, Error? Error)> FindAsync(
        IApplicationDbContext db,
        Guid userId,
        Guid? deviceId,
        CancellationToken cancellationToken)
    {
        if (deviceId is not { } id)
        {
            return (null, NotificationErrors.DeviceRequired());
        }

        var device = await db.UserDevices.FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId && item.RevokedAt == null, cancellationToken);
        return device is null ? (null, NotificationErrors.DeviceNotFound()) : (device, null);
    }
}
