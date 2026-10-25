using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Domain.Administration;
using Situ.Domain.Common;
using Situ.Domain.Reports;

namespace Situ.Application.Features.Reports.ManageReportCategories;

public sealed record CreateReportCategoryRequest(
    Guid ActorId,
    string? IpAddress,
    string Code,
    string Name,
    string Description,
    string ButtonIconUrl,
    string MapIconUrl,
    string ColorHex,
    int SortOrder,
    int TtlMinutes,
    double ImportanceWeight,
    MediaRequirement MediaRequirement,
    Guid? ParentCategoryId,
    bool IsAdultOnly);

public sealed record UpdateReportCategoryRequest(
    Guid ActorId,
    string? IpAddress,
    Guid CategoryId,
    string Name,
    string Description,
    string ButtonIconUrl,
    string MapIconUrl,
    string ColorHex,
    int SortOrder,
    int TtlMinutes,
    double ImportanceWeight,
    MediaRequirement MediaRequirement,
    Guid? ParentCategoryId,
    bool IsAdultOnly);

public sealed record SetReportCategoryActiveRequest(Guid ActorId, string? IpAddress, Guid CategoryId, bool IsActive);

public sealed record ReorderReportCategoriesRequest(Guid ActorId, string? IpAddress, IReadOnlyList<Guid> CategoryIds);

public sealed record UpsertReportCategoryOverrideRequest(
    Guid ActorId,
    string? IpAddress,
    Guid CategoryId,
    Guid? CountryId,
    Guid? CityId,
    bool IsEnabled,
    int? TtlMinutes);

public sealed record UpsertReportCategoryTranslationRequest(
    Guid ActorId,
    string? IpAddress,
    Guid CategoryId,
    string Locale,
    string Name,
    string Description);

public sealed class CreateReportCategoryValidator : AbstractValidator<CreateReportCategoryRequest>
{
    public CreateReportCategoryValidator()
    {
        RuleFor(request => request.Code).NotEmpty().MaximumLength(40);
        RuleFor(request => request.Name).NotEmpty().MaximumLength(60);
        RuleFor(request => request.Description).NotEmpty().MaximumLength(160);
        RuleFor(request => request.ColorHex).NotEmpty();
        RuleFor(request => request.TtlMinutes).InclusiveBetween(5, 60 * 24 * 30);
        RuleFor(request => request.ImportanceWeight).InclusiveBetween(0d, 10d);
    }
}

public sealed class UpdateReportCategoryValidator : AbstractValidator<UpdateReportCategoryRequest>
{
    public UpdateReportCategoryValidator()
    {
        RuleFor(request => request.CategoryId).NotEmpty();
        RuleFor(request => request.Name).NotEmpty().MaximumLength(60);
        RuleFor(request => request.Description).NotEmpty().MaximumLength(160);
        RuleFor(request => request.TtlMinutes).InclusiveBetween(5, 60 * 24 * 30);
        RuleFor(request => request.ImportanceWeight).InclusiveBetween(0d, 10d);
    }
}

public sealed class UpsertReportCategoryOverrideValidator : AbstractValidator<UpsertReportCategoryOverrideRequest>
{
    public UpsertReportCategoryOverrideValidator()
    {
        RuleFor(request => request.CategoryId).NotEmpty();
        RuleFor(request => request).Must(request => request.CountryId is not null || request.CityId is not null)
            .WithMessage("Indica un país o una ciudad.");
        RuleFor(request => request.TtlMinutes).InclusiveBetween(5, 60 * 24 * 30).When(request => request.TtlMinutes is not null);
    }
}

public sealed class UpsertReportCategoryTranslationValidator : AbstractValidator<UpsertReportCategoryTranslationRequest>
{
    public UpsertReportCategoryTranslationValidator()
    {
        RuleFor(request => request.CategoryId).NotEmpty();
        RuleFor(request => request.Locale).NotEmpty().MaximumLength(15);
        RuleFor(request => request.Name).NotEmpty().MaximumLength(60);
        RuleFor(request => request.Description).NotEmpty().MaximumLength(160);
    }
}

public sealed class CreateReportCategoryHandler(IApplicationDbContext db, IClock clock)
    : IHandler<CreateReportCategoryRequest, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateReportCategoryRequest request, CancellationToken cancellationToken)
    {
        if (!await StaffGate.HasAsync(db, request.ActorId, StaffPermissionCodes.CategoriesManage, cancellationToken))
        {
            return Result<Guid>.Failure(ReportErrors.StaffForbidden());
        }

        try
        {
            var category = ReportCategory.Create(
                request.Code,
                request.Name,
                request.Description,
                request.ButtonIconUrl,
                request.MapIconUrl,
                request.ColorHex,
                request.SortOrder,
                request.TtlMinutes,
                request.ImportanceWeight,
                request.MediaRequirement,
                request.ParentCategoryId,
                request.IsAdultOnly);
            category.MarkCreated(clock.UtcNow);
            db.ReportCategories.Add(category);
            await AuditAsync(db, clock, request.ActorId, request.IpAddress, "category.create", category.Id.ToString(), category.Code, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return Result<Guid>.Success(category.Id);
        }
        catch (DomainException exception) when (exception.Code.StartsWith("report_category.", StringComparison.Ordinal))
        {
            return Result<Guid>.Failure(ReportErrors.FromDomain(exception));
        }
        catch (DbUpdateException)
        {
            return Result<Guid>.Failure(ReportErrors.CodeTaken());
        }
    }

    internal static async Task AuditAsync(
        IApplicationDbContext db,
        IClock clock,
        Guid actorId,
        string? ipAddress,
        string action,
        string entityId,
        string? changes,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        db.AuditLogs.Add(AuditLog.Record(actorId, action, nameof(ReportCategory), entityId, changes, ipAddress, now));
        await CatalogVersions.BumpAsync(db, now, cancellationToken);
    }
}

public sealed class UpdateReportCategoryHandler(IApplicationDbContext db, IClock clock)
    : IHandler<UpdateReportCategoryRequest, Result>
{
    public async Task<Result> Handle(UpdateReportCategoryRequest request, CancellationToken cancellationToken)
    {
        if (!await StaffGate.HasAsync(db, request.ActorId, StaffPermissionCodes.CategoriesManage, cancellationToken))
        {
            return Result.Failure(ReportErrors.StaffForbidden());
        }

        var category = await db.ReportCategories.FirstOrDefaultAsync(item => item.Id == request.CategoryId, cancellationToken);
        if (category is null)
        {
            return Result.Failure(ReportErrors.CategoryNotFound());
        }

        try
        {
            category.Update(
                request.Name,
                request.Description,
                request.ButtonIconUrl,
                request.MapIconUrl,
                request.ColorHex,
                request.SortOrder,
                request.TtlMinutes,
                request.ImportanceWeight,
                request.MediaRequirement,
                request.ParentCategoryId,
                request.IsAdultOnly);
            await CreateReportCategoryHandler.AuditAsync(db, clock, request.ActorId, request.IpAddress, "category.update", category.Id.ToString(), category.Code, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DomainException exception)
        {
            return Result.Failure(ReportErrors.FromDomain(exception));
        }
    }
}

public sealed class SetReportCategoryActiveHandler(IApplicationDbContext db, IClock clock)
    : IHandler<SetReportCategoryActiveRequest, Result>
{
    public async Task<Result> Handle(SetReportCategoryActiveRequest request, CancellationToken cancellationToken)
    {
        if (!await StaffGate.HasAsync(db, request.ActorId, StaffPermissionCodes.CategoriesManage, cancellationToken))
        {
            return Result.Failure(ReportErrors.StaffForbidden());
        }

        var category = await db.ReportCategories.FirstOrDefaultAsync(item => item.Id == request.CategoryId, cancellationToken);
        if (category is null)
        {
            return Result.Failure(ReportErrors.CategoryNotFound());
        }

        if (request.IsActive)
        {
            category.Activate();
        }
        else
        {
            category.Deactivate();
        }

        await CreateReportCategoryHandler.AuditAsync(db, clock, request.ActorId, request.IpAddress, request.IsActive ? "category.activate" : "category.deactivate", category.Id.ToString(), null, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class ReorderReportCategoriesHandler(IApplicationDbContext db, IClock clock)
    : IHandler<ReorderReportCategoriesRequest, Result>
{
    public async Task<Result> Handle(ReorderReportCategoriesRequest request, CancellationToken cancellationToken)
    {
        if (!await StaffGate.HasAsync(db, request.ActorId, StaffPermissionCodes.CategoriesManage, cancellationToken))
        {
            return Result.Failure(ReportErrors.StaffForbidden());
        }

        var categories = await db.ReportCategories.ToListAsync(cancellationToken);
        for (var index = 0; index < request.CategoryIds.Count; index++)
        {
            var category = categories.FirstOrDefault(item => item.Id == request.CategoryIds[index]);
            category?.Update(
                category.Name,
                category.Description,
                category.ButtonIconUrl,
                category.MapIconUrl,
                category.ColorHex,
                index,
                category.TtlMinutes,
                category.ImportanceWeight,
                category.MediaRequirement,
                category.ParentCategoryId,
                category.IsAdultOnly);
        }

        await CreateReportCategoryHandler.AuditAsync(db, clock, request.ActorId, request.IpAddress, "category.reorder", "catalog", string.Join(',', request.CategoryIds), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class UpsertReportCategoryOverrideHandler(IApplicationDbContext db, IClock clock)
    : IHandler<UpsertReportCategoryOverrideRequest, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(UpsertReportCategoryOverrideRequest request, CancellationToken cancellationToken)
    {
        if (!await StaffGate.HasAsync(db, request.ActorId, StaffPermissionCodes.CategoriesManage, cancellationToken))
        {
            return Result<Guid>.Failure(ReportErrors.StaffForbidden());
        }

        if (!await db.ReportCategories.AnyAsync(item => item.Id == request.CategoryId, cancellationToken))
        {
            return Result<Guid>.Failure(ReportErrors.CategoryNotFound());
        }

        try
        {
            var existing = await db.ReportCategoryOverrides.FirstOrDefaultAsync(
                item => item.CategoryId == request.CategoryId && item.CountryId == request.CountryId && item.CityId == request.CityId,
                cancellationToken);
            if (existing is null)
            {
                existing = ReportCategoryOverride.Create(request.CategoryId, request.CountryId, request.CityId, request.IsEnabled, request.TtlMinutes);
                existing.MarkCreated(clock.UtcNow);
                db.ReportCategoryOverrides.Add(existing);
            }
            else
            {
                existing.Update(request.IsEnabled, request.TtlMinutes);
            }

            await CreateReportCategoryHandler.AuditAsync(db, clock, request.ActorId, request.IpAddress, "category.override", existing.Id.ToString(), request.IsEnabled ? "enabled" : "disabled", cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return Result<Guid>.Success(existing.Id);
        }
        catch (DomainException exception)
        {
            return Result<Guid>.Failure(ReportErrors.FromDomain(exception));
        }
    }
}

public sealed class UpsertReportCategoryTranslationHandler(IApplicationDbContext db, IClock clock)
    : IHandler<UpsertReportCategoryTranslationRequest, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(UpsertReportCategoryTranslationRequest request, CancellationToken cancellationToken)
    {
        if (!await StaffGate.HasAsync(db, request.ActorId, StaffPermissionCodes.CategoriesManage, cancellationToken))
        {
            return Result<Guid>.Failure(ReportErrors.StaffForbidden());
        }

        if (!await db.ReportCategories.AnyAsync(item => item.Id == request.CategoryId, cancellationToken))
        {
            return Result<Guid>.Failure(ReportErrors.CategoryNotFound());
        }

        var locale = request.Locale.Trim();
        var existing = await db.ReportCategoryTranslations.FirstOrDefaultAsync(
            item => item.CategoryId == request.CategoryId && item.Locale == locale,
            cancellationToken);
        if (existing is null)
        {
            existing = ReportCategoryTranslation.Create(request.CategoryId, locale, request.Name, request.Description);
            existing.MarkCreated(clock.UtcNow);
            db.ReportCategoryTranslations.Add(existing);
        }
        else
        {
            existing.Update(request.Name, request.Description);
        }

        await CreateReportCategoryHandler.AuditAsync(db, clock, request.ActorId, request.IpAddress, "category.translation", existing.Id.ToString(), locale, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(existing.Id);
    }
}
