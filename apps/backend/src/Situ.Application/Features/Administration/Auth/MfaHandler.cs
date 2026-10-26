using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Administration.Staff;
using Situ.Application.Features.Identity;
using Situ.Application.Features.Reports;
using Situ.Domain.Administration;
using Situ.Domain.Identity;

namespace Situ.Application.Features.Administration.Auth;

public sealed record SetupStaffTwoFactorRequest(Guid ActorId, string? IpAddress);

public sealed record SetupStaffTwoFactorResponse(string Secret, string OtpauthUri);

public sealed class SetupStaffTwoFactorHandler(IApplicationDbContext db, IClock clock, ISecretProtector protector)
    : IHandler<SetupStaffTwoFactorRequest, Result<SetupStaffTwoFactorResponse>>
{
    public async Task<Result<SetupStaffTwoFactorResponse>> Handle(SetupStaffTwoFactorRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == request.ActorId, cancellationToken);
        if (user is null || await StaffGate.RoleAsync(db, user.Id, cancellationToken) != "Staff")
        {
            return Result<SetupStaffTwoFactorResponse>.Failure(Error.Unauthorized("mfa.enrollment_invalid", "La inscripción ya no es válida. Vuelve a ingresar."));
        }

        if (user.TwoFactorEnabled)
        {
            return Result<SetupStaffTwoFactorResponse>.Failure(Error.Conflict("mfa.already_enabled", "Esta cuenta ya tiene el segundo factor activo."));
        }

        var secret = Totp.GenerateSecret();
        if (!user.StorePendingTwoFactorSecret(protector.Protect(secret)))
        {
            return Result<SetupStaffTwoFactorResponse>.Failure(Error.Conflict("mfa.already_enabled", "Esta cuenta ya tiene el segundo factor activo."));
        }

        var now = clock.UtcNow;
        StaffSession.Audit(db, user.Id, "staff.mfa_setup", user.Id, new { result = "secret_issued" }, request.IpAddress, now);
        await db.SaveChangesAsync(cancellationToken);
        return Result<SetupStaffTwoFactorResponse>.Success(new SetupStaffTwoFactorResponse(secret, Totp.OtpAuthUri(secret, user.Email.Value)));
    }
}

public sealed record EnableStaffTwoFactorRequest(Guid ActorId, string Code, string? IpAddress, string? UserAgent);

public sealed record EnableStaffTwoFactorResponse(
    IReadOnlyList<string> RecoveryCodes,
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt);

public sealed class EnableStaffTwoFactorValidator : AbstractValidator<EnableStaffTwoFactorRequest>
{
    public EnableStaffTwoFactorValidator()
    {
        RuleFor(request => request.Code).NotEmpty().MaximumLength(12);
    }
}

public sealed class EnableStaffTwoFactorHandler(
    IApplicationDbContext db,
    IClock clock,
    ISecretProtector protector,
    IPasswordHasher passwords,
    ITokenService tokens)
    : IHandler<EnableStaffTwoFactorRequest, Result<EnableStaffTwoFactorResponse>>
{
    public async Task<Result<EnableStaffTwoFactorResponse>> Handle(EnableStaffTwoFactorRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == request.ActorId, cancellationToken);
        if (user is null || await StaffGate.RoleAsync(db, user.Id, cancellationToken) != "Staff" || user.TwoFactorEnabled)
        {
            return Result<EnableStaffTwoFactorResponse>.Failure(Error.Unauthorized("mfa.enrollment_invalid", "La inscripción ya no es válida. Vuelve a ingresar."));
        }

        if (user.IsMfaLocked(now))
        {
            return Result<EnableStaffTwoFactorResponse>.Failure(StaffSession.MfaLocked(user.MfaLockoutEndAt!.Value));
        }

        var secret = StaffSession.PlainSecret(protector, user);
        var check = secret is null ? default : Totp.Check(secret, request.Code, now, user.TwoFactorLastStep);
        if (!check.Accepted)
        {
            return await RejectCodeAsync(user, check.Reused, request.IpAddress, now, cancellationToken);
        }

        user.ConfirmTwoFactor(check.Step);
        var existing = await db.TwoFactorRecoveryCodes.Where(item => item.UserId == user.Id).ToListAsync(cancellationToken);
        db.TwoFactorRecoveryCodes.RemoveRange(existing);
        var plain = new List<string>(Totp.RecoveryCodeCount);
        for (var index = 0; index < Totp.RecoveryCodeCount; index++)
        {
            var code = Totp.CreateRecoveryCode();
            plain.Add(code);
            db.TwoFactorRecoveryCodes.Add(TwoFactorRecoveryCode.Issue(user.Id, passwords.Hash(Totp.NormalizeRecoveryCode(code)), now));
        }

        var session = await StaffSession.IssueAsync(db, tokens, user, request.IpAddress, now, cancellationToken);
        StaffSession.Audit(db, user.Id, "staff.mfa_enabled", user.Id, new { result = "enabled", recoveryCodes = Totp.RecoveryCodeCount }, request.IpAddress, now);
        await db.SaveChangesAsync(cancellationToken);
        return Result<EnableStaffTwoFactorResponse>.Success(new EnableStaffTwoFactorResponse(
            plain,
            session.AccessToken,
            session.RefreshToken,
            session.AccessTokenExpiresAt));
    }

    private async Task<Result<EnableStaffTwoFactorResponse>> RejectCodeAsync(
        User user,
        bool reused,
        string? ipAddress,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        user.RegisterMfaFailure(now);
        var locked = user.IsMfaLocked(now);
        StaffSession.Audit(db, user.Id, locked ? "staff.mfa_locked" : "staff.mfa_failed", user.Id, new { result = locked ? "locked" : reused ? "reused" : "invalid" }, ipAddress, now);
        await db.SaveChangesAsync(cancellationToken);
        return Result<EnableStaffTwoFactorResponse>.Failure(locked ? StaffSession.MfaLocked(user.MfaLockoutEndAt!.Value) : StaffSession.CodeRejected(reused));
    }
}

public sealed record VerifyStaffMfaRequest(string MfaToken, string? Code, string? RecoveryCode, string? IpAddress, string? UserAgent);

public sealed record StaffSessionResponse(string AccessToken, string RefreshToken, DateTimeOffset AccessTokenExpiresAt);

public sealed class VerifyStaffMfaValidator : AbstractValidator<VerifyStaffMfaRequest>
{
    public VerifyStaffMfaValidator()
    {
        RuleFor(request => request.MfaToken).NotEmpty();
        RuleFor(request => request).Must(request => !string.IsNullOrWhiteSpace(request.Code) || !string.IsNullOrWhiteSpace(request.RecoveryCode));
    }
}

public sealed class VerifyStaffMfaHandler(
    IApplicationDbContext db,
    IClock clock,
    ISecretProtector protector,
    IPasswordHasher passwords,
    ITokenService tokens)
    : IHandler<VerifyStaffMfaRequest, Result<StaffSessionResponse>>
{
    public async Task<Result<StaffSessionResponse>> Handle(VerifyStaffMfaRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var ticket = tokens.ReadAccessToken(request.MfaToken.Trim());
        if (ticket is null || ticket.Scope != StaffSession.ChallengeScope || ticket.ExpiresAt <= now)
        {
            return Result<StaffSessionResponse>.Failure(Error.Unauthorized("mfa.token_invalid", "El código de verificación venció. Vuelve a ingresar."));
        }

        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == ticket.UserId, cancellationToken);
        if (user is null || !user.TwoFactorEnabled || await StaffGate.RoleAsync(db, user.Id, cancellationToken) != "Staff")
        {
            return Result<StaffSessionResponse>.Failure(Error.Unauthorized("mfa.token_invalid", "El código de verificación venció. Vuelve a ingresar."));
        }

        if (user.IsMfaLocked(now))
        {
            return Result<StaffSessionResponse>.Failure(StaffSession.MfaLocked(user.MfaLockoutEndAt!.Value));
        }

        if (!string.IsNullOrWhiteSpace(request.RecoveryCode))
        {
            return await UseRecoveryAsync(user, request, now, cancellationToken);
        }

        var secret = StaffSession.PlainSecret(protector, user);
        var check = secret is null ? default : Totp.Check(secret, request.Code ?? "", now, user.TwoFactorLastStep);
        if (!check.Accepted)
        {
            user.RegisterMfaFailure(now);
            var locked = user.IsMfaLocked(now);
            StaffSession.Audit(db, user.Id, locked ? "staff.mfa_locked" : "staff.mfa_failed", user.Id, new { result = locked ? "locked" : check.Reused ? "reused" : "invalid" }, request.IpAddress, now);
            await db.SaveChangesAsync(cancellationToken);
            return Result<StaffSessionResponse>.Failure(locked ? StaffSession.MfaLocked(user.MfaLockoutEndAt!.Value) : StaffSession.CodeRejected(check.Reused));
        }

        user.AcceptTotpStep(check.Step);
        var session = await StaffSession.IssueAsync(db, tokens, user, request.IpAddress, now, cancellationToken);
        StaffSession.Audit(db, user.Id, "staff.mfa_verified", user.Id, new { result = "totp" }, request.IpAddress, now);
        await db.SaveChangesAsync(cancellationToken);
        return Result<StaffSessionResponse>.Success(new StaffSessionResponse(session.AccessToken, session.RefreshToken, session.AccessTokenExpiresAt));
    }

    private async Task<Result<StaffSessionResponse>> UseRecoveryAsync(
        User user,
        VerifyStaffMfaRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var normalized = Totp.NormalizeRecoveryCode(request.RecoveryCode ?? "");
        var codes = await db.TwoFactorRecoveryCodes.Where(item => item.UserId == user.Id && item.UsedAt == null).ToListAsync(cancellationToken);
        var match = codes.FirstOrDefault(item => passwords.Verify(normalized, item.CodeHash));
        if (match is null || !match.TryUse(now))
        {
            user.RegisterMfaFailure(now);
            var locked = user.IsMfaLocked(now);
            StaffSession.Audit(db, user.Id, locked ? "staff.mfa_locked" : "staff.mfa_failed", user.Id, new { result = locked ? "locked" : "recovery_invalid" }, request.IpAddress, now);
            await db.SaveChangesAsync(cancellationToken);
            return Result<StaffSessionResponse>.Failure(locked
                ? StaffSession.MfaLocked(user.MfaLockoutEndAt!.Value)
                : Error.Unauthorized("mfa.recovery_invalid", "El código de recuperación no es válido."));
        }

        user.AcceptTotpStep(user.TwoFactorLastStep ?? now.ToUnixTimeSeconds() / Totp.StepSeconds);
        var session = await StaffSession.IssueAsync(db, tokens, user, request.IpAddress, now, cancellationToken);
        StaffSession.Audit(db, user.Id, "staff.mfa_recovery_used", user.Id, new { result = "recovery" }, request.IpAddress, now);
        await db.SaveChangesAsync(cancellationToken);
        return Result<StaffSessionResponse>.Success(new StaffSessionResponse(session.AccessToken, session.RefreshToken, session.AccessTokenExpiresAt));
    }
}

public sealed record RefreshStaffSessionRequest(string RefreshToken, string? IpAddress);

public sealed class RefreshStaffSessionValidator : AbstractValidator<RefreshStaffSessionRequest>
{
    public RefreshStaffSessionValidator() => RuleFor(request => request.RefreshToken).NotEmpty();
}

public sealed class RefreshStaffSessionHandler(IApplicationDbContext db, IClock clock, ITokenService tokens)
    : IHandler<RefreshStaffSessionRequest, Result<StaffSessionResponse>>
{
    public async Task<Result<StaffSessionResponse>> Handle(RefreshStaffSessionRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var current = await db.RefreshTokens.FirstOrDefaultAsync(token => token.TokenHash == tokens.Hash(request.RefreshToken.Trim()), cancellationToken);
        if (current is null || !current.IsActive(now))
        {
            return Result<StaffSessionResponse>.Failure(AuthErrors.SessionInvalid());
        }

        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == current.UserId, cancellationToken);
        var device = await db.UserDevices.FirstOrDefaultAsync(item => item.Id == current.UserDeviceId, cancellationToken);
        if (user is null || device is null || !device.IsActive || !user.TwoFactorEnabled || await StaffGate.RoleAsync(db, user.Id, cancellationToken) != "Staff")
        {
            current.Revoke(now);
            await db.SaveChangesAsync(cancellationToken);
            return Result<StaffSessionResponse>.Failure(AuthErrors.SessionInvalid());
        }

        user.LiftSuspensionIfExpired(now);
        if (user.EvaluateSignIn(now) != SignInEligibility.Allowed)
        {
            current.Revoke(now);
            await db.SaveChangesAsync(cancellationToken);
            return Result<StaffSessionResponse>.Failure(AuthErrors.SessionInvalid());
        }

        device.Touch(now, request.IpAddress);
        var issued = SessionIssuer.Issue(tokens, user.Id, device.Id, now, role: "Staff", scope: StaffSession.CompletedScope);
        current.RotateTo(issued.Stored.Id, now);
        db.RefreshTokens.Add(issued.Stored);
        await db.SaveChangesAsync(cancellationToken);
        return Result<StaffSessionResponse>.Success(new StaffSessionResponse(issued.AccessToken, issued.RefreshToken, issued.AccessTokenExpiresAt));
    }
}

public sealed record LogoutStaffSessionRequest(string RefreshToken, string? IpAddress);

public sealed class LogoutStaffSessionHandler(IApplicationDbContext db, IClock clock, ITokenService tokens)
    : IHandler<LogoutStaffSessionRequest, Result>
{
    public async Task<Result> Handle(LogoutStaffSessionRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var current = await db.RefreshTokens.FirstOrDefaultAsync(token => token.TokenHash == tokens.Hash(request.RefreshToken.Trim()), cancellationToken);
        if (current is null)
        {
            return Result.Success();
        }

        current.Revoke(now);
        StaffSession.Audit(db, current.UserId, "staff.logout", current.UserId, new { result = "revoked" }, request.IpAddress, now);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record ResetStaffTwoFactorRequest(Guid ActorId, string? IpAddress, Guid UserId, string Reason);

public sealed class ResetStaffTwoFactorValidator : AbstractValidator<ResetStaffTwoFactorRequest>
{
    public ResetStaffTwoFactorValidator() => RuleFor(request => request.Reason).NotEmpty().MaximumLength(500);
}

public sealed class ResetStaffTwoFactorHandler(IApplicationDbContext db, IClock clock)
    : IHandler<ResetStaffTwoFactorRequest, Result>
{
    public async Task<Result> Handle(ResetStaffTwoFactorRequest request, CancellationToken cancellationToken)
    {
        if (await AdminSupport.Allow(db, request.ActorId, StaffPermissionCodes.StaffManage, cancellationToken) is { } denied)
        {
            return Result.Failure(denied);
        }

        if (request.ActorId == request.UserId)
        {
            return Result.Failure(Error.Forbidden("mfa.reset_self", "No puedes restablecer tu propio segundo factor por este camino."));
        }

        var actorIsSuper = await (
            from assignment in db.StaffAssignments
            join role in db.StaffRoles on assignment.RoleId equals role.Id
            where assignment.UserId == request.ActorId && assignment.IsActive && role.Name == StaffDirectory.SuperAdminName
            select role.Id).AnyAsync(cancellationToken);
        if (!actorIsSuper)
        {
            return Result.Failure(AdminSupport.Forbidden());
        }

        var user = await db.Users.FirstOrDefaultAsync(item => item.Id == request.UserId, cancellationToken);
        if (user is null || await StaffGate.RoleAsync(db, user.Id, cancellationToken) != "Staff")
        {
            return Result.Failure(Error.NotFound("staff.user_not_found", "No encontramos esa cuenta del equipo."));
        }

        var now = clock.UtcNow;
        user.ResetTwoFactor();
        var codes = await db.TwoFactorRecoveryCodes.Where(item => item.UserId == user.Id).ToListAsync(cancellationToken);
        db.TwoFactorRecoveryCodes.RemoveRange(codes);
        var sessions = await db.RefreshTokens.Where(token => token.UserId == user.Id && token.RevokedAt == null).ToListAsync(cancellationToken);
        foreach (var session in sessions)
        {
            session.Revoke(now);
        }

        StaffSession.Audit(db, request.ActorId, "staff.mfa_reset", user.Id, new { reason = request.Reason.Trim() }, request.IpAddress, now);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
