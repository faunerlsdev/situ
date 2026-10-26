using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Reports;
using Situ.Domain.Identity;

namespace Situ.Application.Features.Identity.RefreshSession;

public sealed class RefreshSessionHandler(IApplicationDbContext db, IClock clock, ITokenService tokens)
    : IHandler<RefreshSessionRequest, Result<RefreshSessionResponse>>
{
    public async Task<Result<RefreshSessionResponse>> Handle(
        RefreshSessionRequest request,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var current = await db.RefreshTokens
            .FirstOrDefaultAsync(token => token.TokenHash == tokens.Hash(request.RefreshToken.Trim()), cancellationToken);
        if (current is null)
        {
            return Result<RefreshSessionResponse>.Failure(AuthErrors.SessionInvalid());
        }

        if (current.RevokedAt is not null && current.ReplacedByTokenId is not null)
        {
            await RevokeFamilyAsync(current.UserDeviceId, now, cancellationToken);
            return Result<RefreshSessionResponse>.Failure(AuthErrors.SessionInvalid());
        }

        if (!current.IsActive(now))
        {
            return Result<RefreshSessionResponse>.Failure(AuthErrors.SessionInvalid());
        }

        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == current.UserId, cancellationToken);
        var device = await db.UserDevices.FirstOrDefaultAsync(item => item.Id == current.UserDeviceId, cancellationToken);
        if (user is null || device is null || !device.IsActive)
        {
            current.Revoke(now);
            await db.SaveChangesAsync(cancellationToken);
            return Result<RefreshSessionResponse>.Failure(AuthErrors.SessionInvalid());
        }

        user.LiftSuspensionIfExpired(now);
        if (user.EvaluateSignIn(now) != SignInEligibility.Allowed)
        {
            return Result<RefreshSessionResponse>.Failure(AuthErrors.SessionInvalid());
        }

        if (await StaffGate.RoleAsync(db, user.Id, cancellationToken) == "Staff")
        {
            current.Revoke(now);
            await db.SaveChangesAsync(cancellationToken);
            return Result<RefreshSessionResponse>.Failure(AuthErrors.StaffUsePanel());
        }

        device.Touch(now, request.IpAddress);
        var issued = SessionIssuer.Issue(tokens, user.Id, device.Id, now);
        current.RotateTo(issued.Stored.Id, now);
        db.RefreshTokens.Add(issued.Stored);
        await db.SaveChangesAsync(cancellationToken);
        return Result<RefreshSessionResponse>.Success(new RefreshSessionResponse(
            issued.AccessToken,
            issued.RefreshToken,
            issued.AccessTokenExpiresAt,
            device.Id));
    }

    private async Task RevokeFamilyAsync(Guid deviceId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var family = await db.RefreshTokens
            .Where(token => token.UserDeviceId == deviceId && token.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in family)
        {
            token.Revoke(now);
        }

        var device = await db.UserDevices.FirstOrDefaultAsync(item => item.Id == deviceId, cancellationToken);
        device?.Revoke(now);
        await db.SaveChangesAsync(cancellationToken);
    }
}
