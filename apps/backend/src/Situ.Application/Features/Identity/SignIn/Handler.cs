using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Reports;
using Situ.Domain.Administration;
using Situ.Domain.Common;
using Situ.Domain.Identity;
using Situ.Domain.Monetization;

namespace Situ.Application.Features.Identity.SignIn;

public sealed class SignInHandler(IApplicationDbContext db, IClock clock, IPasswordHasher passwords, ITokenService tokens)
    : IHandler<SignInRequest, Result<SignInResponse>>
{
    private const string AbsentPasswordHash =
        "pbkdf2-sha256$100000$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    public async Task<Result<SignInResponse>> Handle(SignInRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var login = request.Login.Trim();
        var user = await FindUserAsync(login, cancellationToken);
        var policy = await LoadLockoutPolicyAsync(cancellationToken);

        if (user is not null)
        {
            user.LiftSuspensionIfExpired(now);
            if (user.EvaluateSignIn(now) == SignInEligibility.Locked)
            {
                return await RejectAsync(user, login, request, LoginFailureReason.AccountLocked, now, user.LockoutEndAt, cancellationToken);
            }
        }

        var passwordMatches = passwords.Verify(request.Password, user?.PasswordHash ?? AbsentPasswordHash);
        if (user is null || !passwordMatches)
        {
            if (user is not null)
            {
                user.RegisterFailedLogin(now, policy);
            }

            var locked = user is not null && user.IsLockedOut(now);
            return await RejectAsync(
                user,
                login,
                request,
                locked ? LoginFailureReason.AccountLocked : LoginFailureReason.InvalidCredentials,
                now,
                locked ? user!.LockoutEndAt : null,
                cancellationToken);
        }

        var eligibility = user.EvaluateSignIn(now);
        if (eligibility == SignInEligibility.PendingGuardianConsent)
        {
            await RecordAsync(user.Id, login, request, false, LoginFailureReason.PendingGuardianConsent, now, cancellationToken);
            var accessExpiresAt = now.AddMinutes(30);
            var accessToken = tokens.CreateAccessToken(user.Id, accessExpiresAt, "guardian_consent");
            return Result<SignInResponse>.Failure(AuthErrors.GuardianConsentRequired(accessToken, accessExpiresAt));
        }

        if (eligibility != SignInEligibility.Allowed)
        {
            return await RejectAsync(user, login, request, ToReason(eligibility), now, user.LockoutEndAt, cancellationToken);
        }

        if (user.EmailConfirmedAt is null)
        {
            await RecordAsync(user.Id, login, request, false, LoginFailureReason.EmailUnconfirmed, now, cancellationToken);
            return Result<SignInResponse>.Failure(AuthErrors.EmailUnconfirmed());
        }

        if (await StaffGate.RoleAsync(db, user.Id, cancellationToken) == "Staff")
        {
            await RecordAsync(user.Id, login, request, false, LoginFailureReason.StaffUsePanel, now, cancellationToken);
            return Result<SignInResponse>.Failure(AuthErrors.StaffUsePanel());
        }

        var deviceResult = await AcceptDeviceAsync(user, request, now, cancellationToken);
        if (deviceResult.IsFailure)
        {
            await RecordAsync(user.Id, login, request, false, LoginFailureReason.DeviceLimitReached, now, cancellationToken);
            return Result<SignInResponse>.Failure(deviceResult.Error!);
        }

        user.RegisterSuccessfulLogin(now);
        var device = deviceResult.Value!;
        var role = await Features.Reports.StaffGate.RoleAsync(db, user.Id, cancellationToken);
        var issued = SessionIssuer.Issue(tokens, user.Id, device.Id, now, role);
        db.RefreshTokens.Add(issued.Stored);
        var previous = await db.RefreshTokens
            .Where(token => token.UserDeviceId == device.Id && token.RevokedAt == null && token.Id != issued.Stored.Id)
            .ToListAsync(cancellationToken);
        foreach (var token in previous)
        {
            token.Revoke(now);
        }

        await RecordAsync(user.Id, login, request, true, null, now, cancellationToken);
        return Result<SignInResponse>.Success(new SignInResponse(
            issued.AccessToken,
            issued.RefreshToken,
            issued.AccessTokenExpiresAt,
            device.Id));
    }

    private async Task<User?> FindUserAsync(string login, CancellationToken cancellationToken)
    {
        if (Email.TryCreate(login, out var email))
        {
            return await db.Users.FirstOrDefaultAsync(user => user.Email == email, cancellationToken);
        }

        if (UserName.TryCreate(login, out var userName))
        {
            return await db.Users.FirstOrDefaultAsync(user => user.UserName == userName, cancellationToken);
        }

        return null;
    }

    private async Task<Result<SignInResponse>> RejectAsync(
        User? user,
        string login,
        SignInRequest request,
        LoginFailureReason reason,
        DateTimeOffset now,
        DateTimeOffset? retryAfter,
        CancellationToken cancellationToken)
    {
        await RecordAsync(user?.Id, login, request, false, reason, now, cancellationToken);
        if (retryAfter is { } until && user is not null && user.IsLockedOut(now))
        {
            return Result<SignInResponse>.Failure(AuthErrors.Locked(until));
        }

        if (user is null || reason == LoginFailureReason.InvalidCredentials)
        {
            return Result<SignInResponse>.Failure(AuthErrors.InvalidCredentials());
        }

        return Result<SignInResponse>.Failure(reason switch
        {
            LoginFailureReason.PendingGuardianConsent => AuthErrors.PendingGuardian(),
            LoginFailureReason.Suspended => AuthErrors.Suspended(),
            _ => AuthErrors.NotAllowed(),
        });
    }

    private async Task RecordAsync(
        Guid? userId,
        string login,
        SignInRequest request,
        bool succeeded,
        LoginFailureReason? reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var attempted = Clip(login, 254) ?? "unknown";
        var attempt = succeeded
            ? LoginAttempt.Success(userId!.Value, attempted, Clip(request.IpAddress, 64), Clip(request.UserAgent, 512), Clip(request.DeviceIdentifier, 128), now)
            : LoginAttempt.Failure(userId, attempted, reason ?? LoginFailureReason.InvalidCredentials, Clip(request.IpAddress, 64), Clip(request.UserAgent, 512), Clip(request.DeviceIdentifier, 128), now);
        db.LoginAttempts.Add(attempt);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Result<UserDevice>> AcceptDeviceAsync(
        User user,
        SignInRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var identifier = request.DeviceIdentifier.Trim();
        var devices = await db.UserDevices.Where(device => device.UserId == user.Id).ToListAsync(cancellationToken);
        var current = devices.FirstOrDefault(device => device.DeviceIdentifier == identifier);
        var activeCount = devices.Count(device => device.IsActive);
        var countsAsNew = current is null || !current.IsActive;
        var limit = await DeviceLimitAsync(user.Id, now, cancellationToken);
        if (countsAsNew && limit is { } max && activeCount >= max)
        {
            return Result<UserDevice>.Failure(AuthErrors.DeviceLimit(
                tokens.CreateAccessToken(user.Id, now.AddMinutes(30), "device_management"),
                now.AddMinutes(30)));
        }

        var ipAddress = Clip(request.IpAddress, 64);
        if (current is null)
        {
            current = UserDevice.Register(
                user.Id,
                identifier,
                request.Platform,
                Clip(request.DeviceName, 100),
                Clip(request.AppVersion, 30),
                ipAddress,
                now);
            db.UserDevices.Add(current);
        }
        else if (!current.IsActive)
        {
            current.Reactivate(now);
            current.Touch(now, ipAddress);
        }
        else
        {
            current.Touch(now, ipAddress);
        }

        current.UpdatePushToken(Clip(request.PushToken, 512));
        return Result<UserDevice>.Success(current);
    }

    private async Task<int?> DeviceLimitAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var subscription = await db.Subscriptions
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.CurrentPeriodEnd)
            .FirstOrDefaultAsync(cancellationToken);
        if (subscription is not null && subscription.GrantsAccess(now))
        {
            var paid = await db.SubscriptionPlans.FirstOrDefaultAsync(plan => plan.Id == subscription.PlanId && plan.IsActive, cancellationToken);
            if (paid is not null)
            {
                return paid.MaxDevices;
            }
        }

        var free = await db.SubscriptionPlans.FirstOrDefaultAsync(plan => plan.Code == "free" && plan.IsActive, cancellationToken);
        if (free is not null)
        {
            return free.MaxDevices;
        }

        var setting = await db.SystemSettings
            .FirstOrDefaultAsync(item => item.Key == SystemSettingKeys.MaxDevicesFree, cancellationToken);
        var raw = setting?.Value ?? SystemSettingKeys.Defaults[SystemSettingKeys.MaxDevicesFree];
        return int.TryParse(raw, out var max) ? max : 2;
    }

    private async Task<LockoutPolicy> LoadLockoutPolicyAsync(CancellationToken cancellationToken)
    {
        var settings = await db.SystemSettings
            .Where(item => item.Key == SystemSettingKeys.LoginMaxFailedAttempts || item.Key == SystemSettingKeys.LoginLockoutMinutes)
            .ToListAsync(cancellationToken);
        var attempts = Read(settings, SystemSettingKeys.LoginMaxFailedAttempts, LockoutPolicy.Default.MaxFailedAttempts);
        var minutes = Read(settings, SystemSettingKeys.LoginLockoutMinutes, (int)LockoutPolicy.Default.LockoutDuration.TotalMinutes);
        return new LockoutPolicy(Math.Max(1, attempts), TimeSpan.FromMinutes(Math.Max(1, minutes)));
    }

    private static int Read(IEnumerable<Situ.Domain.Administration.SystemSetting> settings, string key, int fallback)
    {
        var value = settings.FirstOrDefault(item => item.Key == key)?.Value;
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static LoginFailureReason ToReason(SignInEligibility eligibility) => eligibility switch
    {
        SignInEligibility.Locked => LoginFailureReason.AccountLocked,
        SignInEligibility.Suspended => LoginFailureReason.Suspended,
        SignInEligibility.Banned => LoginFailureReason.Banned,
        SignInEligibility.Deleted => LoginFailureReason.Deleted,
        SignInEligibility.PendingGuardianConsent => LoginFailureReason.PendingGuardianConsent,
        _ => LoginFailureReason.InvalidCredentials,
    };

    private static string? Clip(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
