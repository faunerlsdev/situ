using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;

namespace Situ.Application.Features.Identity.RevokeDevice;

public sealed class RevokeDeviceHandler(IApplicationDbContext db, IClock clock, ICurrentUser currentUser)
    : IHandler<RevokeDeviceRequest, Result>
{
    public async Task<Result> Handle(RevokeDeviceRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure(AuthErrors.Unauthenticated());
        }

        var device = await db.UserDevices
            .FirstOrDefaultAsync(item => item.Id == request.DeviceId && item.UserId == userId, cancellationToken);
        if (device is null)
        {
            return Result.Failure(Error.NotFound("auth.device_not_found", "No encontramos ese dispositivo."));
        }

        if (currentUser.DeviceId == device.Id)
        {
            return Result.Failure(Error.Conflict(
                "auth.current_device",
                "Este es el dispositivo que estás usando. Ciérralo desde Cerrar sesión."));
        }

        var now = clock.UtcNow;
        device.Revoke(now);
        var tokens = await db.RefreshTokens
            .Where(token => token.UserDeviceId == device.Id && token.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in tokens)
        {
            token.Revoke(now);
        }

        await Notifications.DeviceAlerts.ForgetDevicesAsync(db, [device.Id], cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
