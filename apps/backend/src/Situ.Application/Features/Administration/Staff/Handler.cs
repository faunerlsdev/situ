using System.Net.Mail;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Domain.Administration;
using Situ.Domain.Common;
using Situ.Domain.Identity;

namespace Situ.Application.Features.Administration.Staff;

public sealed record GrantStaffRoleRequest(Guid? ActorId, string Email, string Role, string? IpAddress);

public sealed record GrantStaffRoleResponse(Guid UserId, Guid AssignmentId, string RoleName);

public sealed class GrantStaffRoleValidator : AbstractValidator<GrantStaffRoleRequest>
{
    public GrantStaffRoleValidator()
    {
        RuleFor(request => request.Email).NotEmpty().MaximumLength(254);
        RuleFor(request => request.Role).NotEmpty().MaximumLength(60);
    }
}

public sealed class GrantStaffRoleHandler(IApplicationDbContext db, IClock clock)
    : IHandler<GrantStaffRoleRequest, Result<GrantStaffRoleResponse>>
{
    public Task<Result<GrantStaffRoleResponse>> Handle(GrantStaffRoleRequest request, CancellationToken cancellationToken) =>
        StaffDirectory.Grant(db, clock, request, cancellationToken);
}

public sealed record AssignStaffRoleRequest(Guid ActorId, string? IpAddress, string Email, string Role);

public sealed class AssignStaffRoleHandler(IApplicationDbContext db, IClock clock)
    : IHandler<AssignStaffRoleRequest, Result<GrantStaffRoleResponse>>
{
    public async Task<Result<GrantStaffRoleResponse>> Handle(AssignStaffRoleRequest request, CancellationToken cancellationToken)
    {
        if (await AdminSupport.Allow(db, request.ActorId, StaffPermissionCodes.StaffManage, cancellationToken) is { } denied)
        {
            return Result<GrantStaffRoleResponse>.Failure(denied);
        }

        return await StaffDirectory.Grant(db, clock, new GrantStaffRoleRequest(request.ActorId, request.Email, request.Role, request.IpAddress), cancellationToken);
    }
}

public sealed record RevokeStaffRoleRequest(Guid ActorId, string? IpAddress, Guid AssignmentId);

public sealed class RevokeStaffRoleHandler(IApplicationDbContext db, IClock clock)
    : IHandler<RevokeStaffRoleRequest, Result>
{
    public async Task<Result> Handle(RevokeStaffRoleRequest request, CancellationToken cancellationToken)
    {
        if (await AdminSupport.Allow(db, request.ActorId, StaffPermissionCodes.StaffManage, cancellationToken) is { } denied)
        {
            return Result.Failure(denied);
        }

        var assignment = await db.StaffAssignments.FirstOrDefaultAsync(item => item.Id == request.AssignmentId, cancellationToken);
        if (assignment is null || !assignment.IsActive)
        {
            return Result.Success();
        }

        var role = await db.StaffRoles.FirstOrDefaultAsync(item => item.Id == assignment.RoleId, cancellationToken);
        if (role?.Name == StaffDirectory.SuperAdminName)
        {
            var active = await db.StaffAssignments.CountAsync(item => item.RoleId == role.Id && item.IsActive, cancellationToken);
            if (active <= 1)
            {
                return Result.Failure(Error.Conflict("staff.last_super_admin", "Tiene que quedar al menos un superadministrador."));
            }
        }

        var now = clock.UtcNow;
        assignment.Revoke(now);
        AdminSupport.Record(db, request.ActorId, "staff.revoke", nameof(StaffAssignment), assignment.Id.ToString(), AdminSupport.Json(new { before = "active", after = "revoked", role = role?.Name }), request.IpAddress, now);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record ListStaffRequest(Guid ActorId);

public sealed record StaffMemberView(Guid AssignmentId, Guid UserId, string UserName, string RoleName, bool IsActive);

public sealed record ListStaffResponse(IReadOnlyList<StaffMemberView> Staff);

public sealed class ListStaffHandler(IApplicationDbContext db)
    : IHandler<ListStaffRequest, Result<ListStaffResponse>>
{
    public async Task<Result<ListStaffResponse>> Handle(ListStaffRequest request, CancellationToken cancellationToken)
    {
        if (await AdminSupport.Allow(db, request.ActorId, StaffPermissionCodes.StaffManage, cancellationToken) is { } denied)
        {
            return Result<ListStaffResponse>.Failure(denied);
        }

        var rows = await (
            from assignment in db.StaffAssignments
            join role in db.StaffRoles on assignment.RoleId equals role.Id
            join user in db.Users on assignment.UserId equals user.Id
            where assignment.IsActive
            orderby role.Name, user.UserName
            select new StaffMemberView(assignment.Id, user.Id, user.UserName.Value, role.Name, assignment.IsActive)).ToListAsync(cancellationToken);
        return Result<ListStaffResponse>.Success(new ListStaffResponse(rows));
    }
}

internal static class StaffDirectory
{
    public const string SuperAdminName = "Super admin";

    private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["superadmin"] = SuperAdminName,
        ["moderator"] = "Moderador",
        ["moderador"] = "Moderador",
        ["support"] = "Soporte",
        ["soporte"] = "Soporte",
        ["contentmanager"] = "Gestor de contenido",
        ["gestordecontenido"] = "Gestor de contenido",
        ["auditor"] = "Auditor",
        ["finance"] = "Finanzas",
        ["finanzas"] = "Finanzas",
    };

    public static async Task<Result<GrantStaffRoleResponse>> Grant(
        IApplicationDbContext db,
        IClock clock,
        GrantStaffRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (!MailAddress.TryCreate(request.Email.Trim(), out var address))
        {
            return Result<GrantStaffRoleResponse>.Failure(Error.Validation("staff.invalid_email", "El correo no es válido."));
        }

        if (!Email.TryCreate(address.Address, out var email))
        {
            return Result<GrantStaffRoleResponse>.Failure(Error.Validation("staff.invalid_email", "El correo no es válido."));
        }

        var roleName = Resolve(request.Role);
        if (roleName is null)
        {
            return Result<GrantStaffRoleResponse>.Failure(Error.Validation("staff.unknown_role", "Ese rol no existe."));
        }

        var user = await db.Users.FirstOrDefaultAsync(item => item.Email == email, cancellationToken);
        if (user is null)
        {
            var nowMissing = clock.UtcNow;
            AdminSupport.Record(db, request.ActorId, "staff.grant_failed", nameof(User), null, AdminSupport.Json(new { result = "user_not_found", role = roleName }), request.IpAddress, nowMissing);
            await db.SaveChangesAsync(cancellationToken);
            return Result<GrantStaffRoleResponse>.Failure(Error.NotFound("staff.user_not_found", "No hay una cuenta con ese correo."));
        }

        var role = await db.StaffRoles.FirstOrDefaultAsync(item => item.Name == roleName, cancellationToken);
        if (role is null)
        {
            return Result<GrantStaffRoleResponse>.Failure(Error.NotFound("staff.role_not_found", "Ese rol todavía no está sembrado."));
        }

        var now = clock.UtcNow;
        var existing = await db.StaffAssignments.FirstOrDefaultAsync(item => item.UserId == user.Id && item.RoleId == role.Id, cancellationToken);
        if (existing is null)
        {
            existing = StaffAssignment.Assign(user.Id, role.Id, request.ActorId, now);
            db.StaffAssignments.Add(existing);
        }
        else if (!existing.IsActive)
        {
            existing.Reactivate(now);
        }

        AdminSupport.Record(db, request.ActorId, "staff.grant", nameof(StaffAssignment), existing.Id.ToString(), AdminSupport.Json(new { userId = user.Id, role = role.Name, source = request.ActorId is null ? "console" : "panel" }), request.IpAddress, now);
        await db.SaveChangesAsync(cancellationToken);
        return Result<GrantStaffRoleResponse>.Success(new GrantStaffRoleResponse(user.Id, existing.Id, role.Name));
    }

    public static string? Resolve(string role)
    {
        var key = new string(role.Where(character => char.IsLetterOrDigit(character)).ToArray()).ToLowerInvariant();
        return Names.TryGetValue(key, out var name) ? name : null;
    }
}
