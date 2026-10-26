using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Identity;
using Situ.Domain.Administration;
using Situ.Domain.Identity;

namespace Situ.Application.Features.Administration.Auth;

internal static class StaffSession
{
    public const string DeviceIdentifier = "situ-admin-panel";

    public const string CompletedScope = "mfa";

    public const string EnrollmentScope = "mfa_enrollment";

    public const string ChallengeScope = "mfa_challenge";

    public static Error NeutralCredentials() =>
        Error.Unauthorized("auth.invalid_credentials", "Correo o contraseña incorrectos.");

    public static Error MfaLocked(DateTimeOffset until) =>
        Error.Locked("mfa.locked", "Demasiados códigos incorrectos. Espera 15 minutos.", until);

    public static Error CodeRejected(bool reused) =>
        reused
            ? Error.Unauthorized("mfa.code_reused", "Ese código ya se usó. Espera el siguiente.")
            : Error.Unauthorized("mfa.code_invalid", "El código no es válido.");

    public static string LimitedToken(ITokenService tokens, Guid userId, DateTimeOffset now, string scope)
    {
        var expires = now.Add(SessionPolicy.StaffAccessTokenLifetime);
        return tokens.CreateAccessToken(userId, expires, scope: scope);
    }

    public static async Task<UserDevice> PanelDeviceAsync(
        IApplicationDbContext db,
        Guid userId,
        string? ipAddress,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var devices = await db.UserDevices.Where(device => device.UserId == userId).ToListAsync(cancellationToken);
        var current = devices.FirstOrDefault(device => device.DeviceIdentifier == DeviceIdentifier);
        if (current is null)
        {
            current = UserDevice.Register(userId, DeviceIdentifier, DevicePlatform.Web, "Panel", null, Clip(ipAddress), now);
            db.UserDevices.Add(current);
            return current;
        }

        if (!current.IsActive)
        {
            current.Reactivate(now);
        }

        current.Touch(now, Clip(ipAddress));
        return current;
    }

    public static async Task<StaffSessionResult> IssueAsync(
        IApplicationDbContext db,
        ITokenService tokens,
        User user,
        string? ipAddress,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        user.RegisterSuccessfulLogin(now);
        var device = await PanelDeviceAsync(db, user.Id, ipAddress, now, cancellationToken);
        var issued = SessionIssuer.Issue(tokens, user.Id, device.Id, now, role: "Staff", scope: CompletedScope);
        db.RefreshTokens.Add(issued.Stored);
        var previous = await db.RefreshTokens
            .Where(token => token.UserDeviceId == device.Id && token.RevokedAt == null && token.Id != issued.Stored.Id)
            .ToListAsync(cancellationToken);
        foreach (var token in previous)
        {
            token.Revoke(now);
        }

        return new StaffSessionResult(issued.AccessToken, issued.RefreshToken, issued.AccessTokenExpiresAt);
    }

    public static async Task<LockoutPolicy> LockoutAsync(IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var settings = await db.SystemSettings
            .Where(item => item.Key == SystemSettingKeys.LoginMaxFailedAttempts || item.Key == SystemSettingKeys.LoginLockoutMinutes)
            .ToListAsync(cancellationToken);
        var attempts = Read(settings, SystemSettingKeys.LoginMaxFailedAttempts, LockoutPolicy.Default.MaxFailedAttempts);
        var minutes = Read(settings, SystemSettingKeys.LoginLockoutMinutes, (int)LockoutPolicy.Default.LockoutDuration.TotalMinutes);
        return new LockoutPolicy(Math.Max(1, attempts), TimeSpan.FromMinutes(Math.Max(1, minutes)));
    }

    public static void Audit(IApplicationDbContext db, Guid? actorId, string action, Guid? userId, object changes, string? ipAddress, DateTimeOffset now) =>
        AdminSupport.Record(db, actorId, action, nameof(User), userId?.ToString(), AdminSupport.Json(changes), Clip(ipAddress), now);

    public static string? PlainSecret(ISecretProtector protector, User user) =>
        user.TwoFactorSecret is { } stored ? protector.Unprotect(stored) : null;

    private static int Read(IEnumerable<SystemSetting> settings, string key, int fallback)
    {
        var value = settings.FirstOrDefault(item => item.Key == key)?.Value;
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static string? Clip(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= 64 ? trimmed : trimmed[..64];
    }
}

internal sealed record StaffSessionResult(string AccessToken, string RefreshToken, DateTimeOffset AccessTokenExpiresAt);
