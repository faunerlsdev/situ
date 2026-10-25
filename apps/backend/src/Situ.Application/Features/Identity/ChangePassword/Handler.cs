using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Guardianship.RevokeGuardianConsent;
using Situ.Application.Features.Notifications;
using Situ.Domain.Administration;
using Situ.Domain.Identity;

namespace Situ.Application.Features.Identity.ChangePassword;

public sealed class ChangePasswordHandler(
    IApplicationDbContext db,
    IClock clock,
    IPasswordHasher passwords,
    ICurrentUser currentUser,
    IEmailSender email)
    : IHandler<ChangePasswordRequest, Result>
{
    public async Task<Result> Handle(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result.Failure(AuthErrors.Unauthenticated());
        }

        if (!PasswordPolicy.IsValid(request.NewPassword))
        {
            return Result.Failure(IdentityErrors.InvalidPassword());
        }

        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null || user.Status == UserStatus.Deleted)
        {
            return Result.Failure(AuthErrors.Unauthenticated());
        }

        var now = clock.UtcNow;
        if (!passwords.Verify(request.CurrentPassword, user.PasswordHash))
        {
            var policy = await LockoutAsync(cancellationToken);
            user.RegisterFailedLogin(now, policy);
            await db.SaveChangesAsync(cancellationToken);
            if (user.IsLockedOut(now) && user.LockoutEndAt is { } until)
            {
                return Result.Failure(AuthErrors.Locked(until));
            }

            return Result.Failure(IdentityErrors.CurrentPasswordInvalid());
        }

        user.ChangePassword(passwords.Hash(request.NewPassword), now);
        await InvalidateResetsAsync(userId, now, cancellationToken);
        var keepCurrent = request.KeepCurrentSession ?? true;
        await CloseSessionsAsync(userId, keepCurrent ? currentUser.DeviceId : null, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await RevokeGuardianConsentHandler.TrySendAsync(
            email,
            user.Email.Value,
            "Cambiamos tu contraseña de SITU",
            "Si no fuiste tú, restablece la contraseña desde la app.",
            cancellationToken);
        return Result.Success();
    }

    private async Task<LockoutPolicy> LockoutAsync(CancellationToken cancellationToken)
    {
        var settings = await db.SystemSettings
            .Where(item => item.Key == SystemSettingKeys.LoginMaxFailedAttempts || item.Key == SystemSettingKeys.LoginLockoutMinutes)
            .ToDictionaryAsync(item => item.Key, item => item.Value, cancellationToken);
        var attempts = Read(settings, SystemSettingKeys.LoginMaxFailedAttempts, LockoutPolicy.Default.MaxFailedAttempts);
        var minutes = Read(settings, SystemSettingKeys.LoginLockoutMinutes, (int)LockoutPolicy.Default.LockoutDuration.TotalMinutes);
        return new LockoutPolicy(Math.Max(1, attempts), TimeSpan.FromMinutes(Math.Max(1, minutes)));
    }

    private static int Read(IReadOnlyDictionary<string, string> settings, string key, int fallback) =>
        settings.TryGetValue(key, out var raw) && int.TryParse(raw, out var value) ? value : fallback;

    private async Task InvalidateResetsAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var tokens = await db.SecurityTokens
            .Where(token => token.UserId == userId && token.Purpose == SecurityTokenPurpose.PasswordReset && token.ConsumedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in tokens)
        {
            token.Invalidate(now);
        }
    }

    private async Task CloseSessionsAsync(Guid userId, Guid? keepDeviceId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var devices = await db.UserDevices
            .Where(device => device.UserId == userId && device.RevokedAt == null)
            .ToListAsync(cancellationToken);
        var closing = devices.Where(device => keepDeviceId is null || device.Id != keepDeviceId).ToList();
        foreach (var device in closing)
        {
            device.Revoke(now);
        }

        var tokens = await db.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in tokens)
        {
            if (keepDeviceId is null || token.UserDeviceId != keepDeviceId)
            {
                token.Revoke(now);
            }
        }

        if (closing.Count > 0)
        {
            await DeviceAlerts.ForgetDevicesAsync(db, closing.Select(device => device.Id).ToArray(), cancellationToken);
        }
    }
}
