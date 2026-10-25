using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Domain.Administration;
using Situ.Domain.Common;
using Situ.Domain.Retention;

namespace Situ.Application.Features.Administration.Retention;

public sealed record ListLegalHoldsRequest(Guid ActorId, bool ActiveOnly);

public sealed record LegalHoldView(Guid Id, string TargetType, Guid TargetId, string Reason, bool Active, DateTimeOffset CreatedAt, DateTimeOffset? ReleasedAt);

public sealed record ListLegalHoldsResponse(IReadOnlyList<LegalHoldView> Holds);

public sealed class ListLegalHoldsHandler(IApplicationDbContext db)
    : IHandler<ListLegalHoldsRequest, Result<ListLegalHoldsResponse>>
{
    public async Task<Result<ListLegalHoldsResponse>> Handle(ListLegalHoldsRequest request, CancellationToken cancellationToken)
    {
        if (await AdminSupport.Allow(db, request.ActorId, StaffPermissionCodes.LegalHoldsManage, cancellationToken) is { } denied)
        {
            return Result<ListLegalHoldsResponse>.Failure(denied);
        }

        var query = db.LegalHolds.AsQueryable();
        if (request.ActiveOnly)
        {
            query = query.Where(item => item.ReleasedAt == null);
        }

        var rows = await query.OrderByDescending(item => item.CreatedAt).Take(100)
            .Select(item => new LegalHoldView(item.Id, item.TargetType.ToString(), item.TargetId, item.Reason, item.ReleasedAt == null, item.CreatedAt, item.ReleasedAt))
            .ToListAsync(cancellationToken);
        return Result<ListLegalHoldsResponse>.Success(new ListLegalHoldsResponse(rows));
    }
}

public sealed record PlaceLegalHoldRequest(Guid ActorId, string? IpAddress, string TargetType, Guid TargetId, string Reason);

public sealed class PlaceLegalHoldValidator : AbstractValidator<PlaceLegalHoldRequest>
{
    public PlaceLegalHoldValidator()
    {
        RuleFor(request => request.TargetType).NotEmpty();
        RuleFor(request => request.TargetId).NotEmpty();
        RuleFor(request => request.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class PlaceLegalHoldHandler(IApplicationDbContext db, IClock clock)
    : IHandler<PlaceLegalHoldRequest, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(PlaceLegalHoldRequest request, CancellationToken cancellationToken)
    {
        if (await AdminSupport.Allow(db, request.ActorId, StaffPermissionCodes.LegalHoldsManage, cancellationToken) is { } denied)
        {
            return Result<Guid>.Failure(denied);
        }

        if (!Enum.TryParse<ContentTargetType>(request.TargetType, out var targetType))
        {
            return Result<Guid>.Failure(Error.Validation("legal_hold.invalid_target", "Indica sobre qué recae el bloqueo."));
        }

        var now = clock.UtcNow;
        LegalHold hold;
        try
        {
            hold = LegalHold.Place(targetType, request.TargetId, request.Reason.Trim(), request.ActorId, now);
        }
        catch (DomainException)
        {
            return Result<Guid>.Failure(Error.Validation("legal_hold.invalid", "Revisa el motivo del bloqueo."));
        }

        db.LegalHolds.Add(hold);
        AdminSupport.Record(db, request.ActorId, "legal_hold.place", nameof(LegalHold), hold.Id.ToString(), AdminSupport.Json(new { before = (string?)null, after = request.Reason.Trim(), targetType = targetType.ToString(), targetId = request.TargetId }), request.IpAddress, now);
        await db.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(hold.Id);
    }
}

public sealed record ReleaseLegalHoldRequest(Guid ActorId, string? IpAddress, Guid HoldId, string Reason);

public sealed class ReleaseLegalHoldValidator : AbstractValidator<ReleaseLegalHoldRequest>
{
    public ReleaseLegalHoldValidator()
    {
        RuleFor(request => request.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class ReleaseLegalHoldHandler(IApplicationDbContext db, IClock clock)
    : IHandler<ReleaseLegalHoldRequest, Result>
{
    public async Task<Result> Handle(ReleaseLegalHoldRequest request, CancellationToken cancellationToken)
    {
        if (await AdminSupport.Allow(db, request.ActorId, StaffPermissionCodes.LegalHoldsManage, cancellationToken) is { } denied)
        {
            return Result.Failure(denied);
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Result.Failure(Error.Validation("legal_hold.reason_required", "Escribe por qué se levanta el bloqueo."));
        }

        var hold = await db.LegalHolds.FirstOrDefaultAsync(item => item.Id == request.HoldId, cancellationToken);
        if (hold is null)
        {
            return Result.Failure(Error.NotFound("legal_hold.not_found", "No encontramos el bloqueo."));
        }

        var now = clock.UtcNow;
        hold.Release(now);
        AdminSupport.Record(db, request.ActorId, "legal_hold.release", nameof(LegalHold), hold.Id.ToString(), AdminSupport.Json(new { before = "active", after = "released", reason = request.Reason.Trim() }), request.IpAddress, now);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record ListRetentionPoliciesRequest(Guid ActorId);

public sealed record RetentionPolicyView(string Kind, int RetainDays, bool IsActive);

public sealed record ListRetentionPoliciesResponse(IReadOnlyList<RetentionPolicyView> Policies);

public sealed class ListRetentionPoliciesHandler(IApplicationDbContext db)
    : IHandler<ListRetentionPoliciesRequest, Result<ListRetentionPoliciesResponse>>
{
    public async Task<Result<ListRetentionPoliciesResponse>> Handle(ListRetentionPoliciesRequest request, CancellationToken cancellationToken)
    {
        if (await AdminSupport.Allow(db, request.ActorId, StaffPermissionCodes.LegalHoldsManage, cancellationToken) is { } denied)
        {
            return Result<ListRetentionPoliciesResponse>.Failure(denied);
        }

        var rows = await db.RetentionPolicies.OrderBy(item => item.Kind)
            .Select(item => new RetentionPolicyView(item.Kind.ToString(), item.RetainDays, item.IsActive))
            .ToListAsync(cancellationToken);
        return Result<ListRetentionPoliciesResponse>.Success(new ListRetentionPoliciesResponse(rows));
    }
}

public sealed record UpdateRetentionPolicyRequest(Guid ActorId, string? IpAddress, string Kind, int RetainDays);

public sealed class UpdateRetentionPolicyValidator : AbstractValidator<UpdateRetentionPolicyRequest>
{
    public UpdateRetentionPolicyValidator()
    {
        RuleFor(request => request.Kind).NotEmpty();
        RuleFor(request => request.RetainDays).InclusiveBetween(1, 3650);
    }
}

public sealed class UpdateRetentionPolicyHandler(IApplicationDbContext db, IClock clock)
    : IHandler<UpdateRetentionPolicyRequest, Result>
{
    public async Task<Result> Handle(UpdateRetentionPolicyRequest request, CancellationToken cancellationToken)
    {
        if (await AdminSupport.Allow(db, request.ActorId, StaffPermissionCodes.LegalHoldsManage, cancellationToken) is { } denied)
        {
            return Result.Failure(denied);
        }

        if (!Enum.TryParse<RetentionDataKind>(request.Kind, out var kind))
        {
            return Result.Failure(Error.Validation("retention.unknown_kind", "Ese tipo de dato no existe."));
        }

        var now = clock.UtcNow;
        var policy = await db.RetentionPolicies.FirstOrDefaultAsync(item => item.Kind == kind, cancellationToken);
        var before = policy?.RetainDays;
        try
        {
            if (policy is null)
            {
                policy = RetentionPolicy.Create(kind, request.RetainDays, now);
                db.RetentionPolicies.Add(policy);
            }
            else
            {
                policy.ChangeRetention(request.RetainDays, now);
            }
        }
        catch (DomainException)
        {
            return Result.Failure(Error.Validation("retention.invalid_days", "Los días de retención tienen que estar entre 1 y 3650."));
        }

        AdminSupport.Record(db, request.ActorId, "retention.update", nameof(RetentionPolicy), kind.ToString(), AdminSupport.Json(new { before, after = policy.RetainDays }), request.IpAddress, now);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
