using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;

namespace Situ.Application.Features.Notifications;

internal static class DeviceAlerts
{
    public static async Task ForgetDevicesAsync(IApplicationDbContext db, IReadOnlyCollection<Guid> deviceIds, CancellationToken cancellationToken)
    {
        if (deviceIds.Count == 0)
        {
            return;
        }

        var rows = await db.DeviceAlertLocations.Where(item => deviceIds.Contains(item.DeviceId)).ToListAsync(cancellationToken);
        db.DeviceAlertLocations.RemoveRange(rows);
    }

    public static async Task ForgetUserAsync(IApplicationDbContext db, Guid userId, CancellationToken cancellationToken)
    {
        var rows = await db.DeviceAlertLocations.Where(item => item.UserId == userId).ToListAsync(cancellationToken);
        db.DeviceAlertLocations.RemoveRange(rows);
    }
}
