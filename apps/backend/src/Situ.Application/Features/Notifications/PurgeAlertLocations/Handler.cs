using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;

namespace Situ.Application.Features.Notifications.PurgeAlertLocations;

public sealed record PurgeAlertLocationsRequest;
public sealed record PurgeAlertLocationsResponse(int Removed);

public sealed class PurgeAlertLocationsHandler(IApplicationDbContext db, IClock clock)
    : IHandler<PurgeAlertLocationsRequest, Result<PurgeAlertLocationsResponse>>
{
    public async Task<Result<PurgeAlertLocationsResponse>> Handle(PurgeAlertLocationsRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var stale = await db.DeviceAlertLocations.Where(item => item.ExpiresAt <= now).ToListAsync(cancellationToken);
        db.DeviceAlertLocations.RemoveRange(stale);
        if (stale.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return Result<PurgeAlertLocationsResponse>.Success(new PurgeAlertLocationsResponse(stale.Count));
    }
}
