using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;

namespace Situ.Application.Features.Identity.SignOutAllDevices;

public sealed class SignOutAllDevicesHandler(IApplicationDbContext db, IClock clock, ICurrentUser currentUser)
    : IHandler<SignOutAllDevicesRequest, Result>
{
    public async Task<Result> Handle(SignOutAllDevicesRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure(AuthErrors.Unauthenticated());
        }

        var now = clock.UtcNow;
        var tokens = await db.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in tokens)
        {
            token.Revoke(now);
        }

        var devices = await db.UserDevices
            .Where(device => device.UserId == userId && device.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var device in devices)
        {
            device.Revoke(now);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
