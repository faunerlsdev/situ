using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;

namespace Situ.Application.Features.MapRealtime.AuthorizeMapConnection;

public sealed record AuthorizeMapConnectionRequest(Guid UserId, Guid? DeviceId, bool IsStaff, DateTimeOffset? AccessExpiresAt);

public sealed record AuthorizeMapConnectionResponse(DateTimeOffset AccessExpiresAt);

public sealed class AuthorizeMapConnectionHandler(IApplicationDbContext db, IClock clock)
    : IHandler<AuthorizeMapConnectionRequest, Result<AuthorizeMapConnectionResponse>>
{
    public async Task<Result<AuthorizeMapConnectionResponse>> Handle(
        AuthorizeMapConnectionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.IsStaff)
        {
            return Result<AuthorizeMapConnectionResponse>.Failure(
                Error.Forbidden("map.staff_forbidden", "Esta cuenta no usa el mapa en tiempo real."));
        }

        if (request.DeviceId is not { } deviceId)
        {
            return Result<AuthorizeMapConnectionResponse>.Failure(
                Error.Unauthorized("map.device_required", "No se pudo abrir el mapa en tiempo real."));
        }

        var now = clock.UtcNow;
        if (request.AccessExpiresAt is not { } expiresAt || expiresAt <= now)
        {
            return Result<AuthorizeMapConnectionResponse>.Failure(
                Error.Unauthorized("map.token_expired", "No se pudo abrir el mapa en tiempo real."));
        }

        var device = await db.UserDevices.FirstOrDefaultAsync(
            item => item.Id == deviceId && item.UserId == request.UserId,
            cancellationToken);
        if (device is null || !device.IsActive)
        {
            return Result<AuthorizeMapConnectionResponse>.Failure(
                Error.Unauthorized("map.device_revoked", "No se pudo abrir el mapa en tiempo real."));
        }

        return Result<AuthorizeMapConnectionResponse>.Success(new AuthorizeMapConnectionResponse(expiresAt));
    }
}
