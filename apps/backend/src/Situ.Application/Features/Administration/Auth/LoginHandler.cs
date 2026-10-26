using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Identity;
using Situ.Application.Features.Reports;
using Situ.Domain.Common;
using Situ.Domain.Identity;

namespace Situ.Application.Features.Administration.Auth;

public sealed record AdminLoginRequest(string Email, string Password, string? IpAddress, string? UserAgent);

public sealed record AdminLoginResponse(
    bool MfaEnrollmentRequired,
    bool MfaRequired,
    string? EnrollmentToken,
    DateTimeOffset? EnrollmentTokenExpiresAt,
    string? MfaToken,
    DateTimeOffset? MfaTokenExpiresAt);

public sealed class AdminLoginValidator : AbstractValidator<AdminLoginRequest>
{
    public AdminLoginValidator()
    {
        RuleFor(request => request.Email).NotEmpty().MaximumLength(254);
        RuleFor(request => request.Password).NotEmpty().MaximumLength(200);
    }
}

public sealed class AdminLoginHandler(
    IApplicationDbContext db,
    IClock clock,
    IPasswordHasher passwords,
    ITokenService tokens)
    : IHandler<AdminLoginRequest, Result<AdminLoginResponse>>
{
    private const string AbsentPasswordHash =
        "pbkdf2-sha256$100000$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    public async Task<Result<AdminLoginResponse>> Handle(AdminLoginRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var email = request.Email.Trim();
        User? user = null;
        if (Email.TryCreate(email, out var parsed))
        {
            user = await db.Users.FirstOrDefaultAsync(item => item.Email == parsed, cancellationToken);
        }

        var policy = await StaffSession.LockoutAsync(db, cancellationToken);
        if (user is not null)
        {
            user.LiftSuspensionIfExpired(now);
            if (user.EvaluateSignIn(now) == SignInEligibility.Locked)
            {
                return await RejectAsync(user, email, request, LoginFailureReason.AccountLocked, now, user.LockoutEndAt, cancellationToken);
            }
        }

        var since = now.AddMinutes(-10);
        var recent = await db.LoginAttempts.CountAsync(
            attempt => attempt.EmailAttempted == email && !attempt.Succeeded && attempt.OccurredAt >= since,
            cancellationToken);
        if (recent >= 5)
        {
            return Result<AdminLoginResponse>.Failure(Error.RateLimited(
                "auth.rate_limited",
                "Demasiados intentos. Espera un momento.",
                now.AddMinutes(10)));
        }

        var passwordMatches = passwords.Verify(request.Password, user?.PasswordHash ?? AbsentPasswordHash);
        if (user is null || !passwordMatches || await StaffGate.RoleAsync(db, user.Id, cancellationToken) != "Staff")
        {
            if (user is not null && !passwordMatches)
            {
                user.RegisterFailedLogin(now, policy);
            }

            var locked = user is not null && !passwordMatches && user.IsLockedOut(now);
            return await RejectAsync(
                user,
                email,
                request,
                locked ? LoginFailureReason.AccountLocked : LoginFailureReason.InvalidCredentials,
                now,
                locked ? user!.LockoutEndAt : null,
                cancellationToken);
        }

        var eligibility = user.EvaluateSignIn(now);
        if (eligibility != SignInEligibility.Allowed || user.EmailConfirmedAt is null)
        {
            var reason = user.EmailConfirmedAt is null ? LoginFailureReason.EmailUnconfirmed : ToReason(eligibility);
            return await RejectAsync(user, email, request, reason, now, user.LockoutEndAt, cancellationToken);
        }

        user.Unlock();
        if (!user.TwoFactorEnabled || string.IsNullOrEmpty(user.TwoFactorSecret))
        {
            var enrollment = StaffSession.LimitedToken(tokens, user.Id, now, StaffSession.EnrollmentScope);
            Record(user.Id, email, request, true, null, now);
            StaffSession.Audit(db, user.Id, "staff.mfa_enrollment_required", user.Id, new { result = "enrollment" }, request.IpAddress, now);
            await db.SaveChangesAsync(cancellationToken);
            return Result<AdminLoginResponse>.Success(new AdminLoginResponse(
                true,
                false,
                enrollment,
                now.Add(SessionPolicy.StaffAccessTokenLifetime),
                null,
                null));
        }

        if (user.IsMfaLocked(now))
        {
            StaffSession.Audit(db, user.Id, "staff.mfa_locked", user.Id, new { result = "locked" }, request.IpAddress, now);
            await db.SaveChangesAsync(cancellationToken);
            return Result<AdminLoginResponse>.Failure(StaffSession.MfaLocked(user.MfaLockoutEndAt!.Value));
        }

        var challenge = StaffSession.LimitedToken(tokens, user.Id, now, StaffSession.ChallengeScope);
        Record(user.Id, email, request, true, null, now);
        StaffSession.Audit(db, user.Id, "staff.mfa_challenged", user.Id, new { result = "challenge" }, request.IpAddress, now);
        await db.SaveChangesAsync(cancellationToken);
        return Result<AdminLoginResponse>.Success(new AdminLoginResponse(
            false,
            true,
            null,
            null,
            challenge,
            now.Add(SessionPolicy.StaffAccessTokenLifetime)));
    }

    private async Task<Result<AdminLoginResponse>> RejectAsync(
        User? user,
        string email,
        AdminLoginRequest request,
        LoginFailureReason reason,
        DateTimeOffset now,
        DateTimeOffset? retryAfter,
        CancellationToken cancellationToken)
    {
        Record(user?.Id, email, request, false, reason, now);
        StaffSession.Audit(db, user?.Id, "staff.login_failed", user?.Id, new { result = "rejected" }, request.IpAddress, now);
        await db.SaveChangesAsync(cancellationToken);
        if (retryAfter is { } until && user is not null && user.IsLockedOut(now))
        {
            return Result<AdminLoginResponse>.Failure(AuthErrors.Locked(until));
        }

        if (reason == LoginFailureReason.EmailUnconfirmed)
        {
            return Result<AdminLoginResponse>.Failure(AuthErrors.EmailUnconfirmed());
        }

        if (reason == LoginFailureReason.Suspended)
        {
            return Result<AdminLoginResponse>.Failure(AuthErrors.Suspended());
        }

        if (reason is LoginFailureReason.Banned or LoginFailureReason.Deleted)
        {
            return Result<AdminLoginResponse>.Failure(AuthErrors.NotAllowed());
        }

        return Result<AdminLoginResponse>.Failure(StaffSession.NeutralCredentials());
    }

    private void Record(Guid? userId, string email, AdminLoginRequest request, bool succeeded, LoginFailureReason? reason, DateTimeOffset now)
    {
        var attempted = string.IsNullOrWhiteSpace(email) ? "unknown" : email.Trim();
        if (attempted.Length > 254)
        {
            attempted = attempted[..254];
        }

        var agent = string.IsNullOrWhiteSpace(request.UserAgent) ? null : request.UserAgent.Trim();
        if (agent is { Length: > 512 })
        {
            agent = agent[..512];
        }

        db.LoginAttempts.Add(succeeded
            ? LoginAttempt.Success(userId!.Value, attempted, Clip(request.IpAddress), agent, StaffSession.DeviceIdentifier, now)
            : LoginAttempt.Failure(userId, attempted, reason ?? LoginFailureReason.InvalidCredentials, Clip(request.IpAddress), agent, StaffSession.DeviceIdentifier, now));
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
