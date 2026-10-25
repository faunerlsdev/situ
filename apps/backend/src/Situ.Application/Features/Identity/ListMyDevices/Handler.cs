using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;

namespace Situ.Application.Features.Identity.ListMyDevices;

public sealed class ListMyDevicesHandler(IApplicationDbContext db, ICurrentUser currentUser)
    : IHandler<ListMyDevicesRequest, Result<ListMyDevicesResponse>>
{
    public async Task<Result<ListMyDevicesResponse>> Handle(
        ListMyDevicesRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result<ListMyDevicesResponse>.Failure(AuthErrors.Unauthenticated());
        }

        var stored = await db.UserDevices
            .AsNoTracking()
            .Where(device => device.UserId == userId)
            .ToListAsync(cancellationToken);
        var devices = stored
            .OrderByDescending(device => device.IsActive)
            .ThenByDescending(device => device.LastSeenAt)
            .Select(device => new DeviceItem(
                device.Id,
                device.DeviceIdentifier,
                device.Platform,
                device.DeviceName,
                device.LastSeenAt,
                device.IsActive,
                device.Id == currentUser.DeviceId))
            .ToArray();

        return Result<ListMyDevicesResponse>.Success(new ListMyDevicesResponse(devices));
    }
}
