using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Reports;
using Situ.Domain.Common;
using Situ.Domain.Identity;

namespace Situ.Application.Features.Administration.Auth;

/// <summary>
/// Server-console reset of one staff authenticator. Not an HTTP handler and not registered as one.
/// </summary>
public static class StaffTwoFactorConsole
{
    public const string Action = "staff.2fa_reset_console";

    public static async Task<Result> ResetAsync(
        IApplicationDbContext db,
        IClock clock,
        string email,
        CancellationToken cancellationToken)
    {
        if (!Email.TryCreate(email, out var address))
        {
            return Result.Failure(Error.Validation("staff.invalid_email", "El correo no es válido."));
        }

        var now = clock.UtcNow;
        var user = await db.Users.FirstOrDefaultAsync(item => item.Email == address, cancellationToken);
        if (user is null)
        {
            AdminSupport.Record(db, null, Action, nameof(User), null, AdminSupport.Json(new { result = "user_not_found" }), null, now);
            await db.SaveChangesAsync(cancellationToken);
            return Result.Failure(Error.NotFound("staff.user_not_found", "No hay una cuenta con ese correo."));
        }

        if (await StaffGate.RoleAsync(db, user.Id, cancellationToken) != "Staff")
        {
            AdminSupport.Record(db, null, Action, nameof(User), user.Id.ToString(), AdminSupport.Json(new { result = "not_staff" }), null, now);
            await db.SaveChangesAsync(cancellationToken);
            return Result.Failure(Error.Forbidden("staff.not_staff", "Esa cuenta no es del equipo."));
        }

        user.ResetTwoFactor();
        var codes = await db.TwoFactorRecoveryCodes.Where(item => item.UserId == user.Id).ToListAsync(cancellationToken);
        db.TwoFactorRecoveryCodes.RemoveRange(codes);
        var sessions = await db.RefreshTokens.Where(token => token.UserId == user.Id && token.RevokedAt == null).ToListAsync(cancellationToken);
        foreach (var session in sessions)
        {
            session.Revoke(now);
        }

        AdminSupport.Record(db, null, Action, nameof(User), user.Id.ToString(), AdminSupport.Json(new { result = "reset" }), null, now);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
