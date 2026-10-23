using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;

namespace Situ.Application.Features.Identity.SignOut;

public sealed class SignOutHandler(IApplicationDbContext db, IClock clock, ITokenService tokens)
    : IHandler<SignOutRequest, Result>
{
    public async Task<Result> Handle(SignOutRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var current = await db.RefreshTokens
            .FirstOrDefaultAsync(token => token.TokenHash == tokens.Hash(request.RefreshToken.Trim()), cancellationToken);
        if (current is null)
        {
            return Result.Success();
        }

        var family = await db.RefreshTokens
            .Where(token => token.UserDeviceId == current.UserDeviceId && token.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in family)
        {
            token.Revoke(now);
        }

        current.Revoke(now);
        var device = await db.UserDevices.FirstOrDefaultAsync(item => item.Id == current.UserDeviceId, cancellationToken);
        device?.Revoke(now);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
