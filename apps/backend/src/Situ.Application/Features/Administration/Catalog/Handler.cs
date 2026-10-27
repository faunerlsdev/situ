using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Reports;
using Situ.Domain.Administration;
using Situ.Domain.Common;
using Situ.Domain.Profiles;
using Situ.Domain.Reports;

namespace Situ.Application.Features.Administration.Catalog;

public sealed record ListAdminCategoriesRequest(Guid ActorId);

public sealed record GetAdminCategoryRequest(Guid ActorId, Guid CategoryId);

public sealed record AdminTranslationView(string Locale, string Name, string Description);

public sealed record AdminOverrideView(Guid? CountryId, Guid? CityId, bool IsEnabled, int? TtlMinutes);

public sealed record AdminCategoryView(
    Guid Id,
    string Code,
    string Name,
    string Description,
    string ButtonIconUrl,
    string MapIconUrl,
    string ColorHex,
    int SortOrder,
    bool IsActive,
    int TtlMinutes,
    double ImportanceWeight,
    string MediaRequirement,
    bool IsAdultOnly,
    int ActiveReportCount,
    IReadOnlyList<AdminTranslationView> Translations,
    IReadOnlyList<AdminOverrideView> Overrides);

public sealed record ListAdminCategoriesResponse(IReadOnlyList<AdminCategoryView> Categories, int CatalogVersion);

public sealed class ListAdminCategoriesHandler(IApplicationDbContext db, IClock clock)
    : IHandler<ListAdminCategoriesRequest, Result<ListAdminCategoriesResponse>>
{
    public async Task<Result<ListAdminCategoriesResponse>> Handle(ListAdminCategoriesRequest request, CancellationToken cancellationToken)
    {
        if (await AdminSupport.Allow(db, request.ActorId, StaffPermissionCodes.CategoriesManage, cancellationToken) is { } denied)
        {
            return Result<ListAdminCategoriesResponse>.Failure(denied);
        }

        var categories = await db.ReportCategories.OrderBy(item => item.SortOrder).ToListAsync(cancellationToken);
        var translations = await db.ReportCategoryTranslations.ToListAsync(cancellationToken);
        var overrides = await db.ReportCategoryOverrides.ToListAsync(cancellationToken);
        var counts = await ActiveReportCounts.LoadAsync(db, clock, cancellationToken);
        var settings = await ReportSettings.LoadAsync(db, cancellationToken);
        var version = ReportSettings.Int(settings, SystemSettingKeys.ReportCatalogVersion, 1);
        var views = categories.Select(item => CategoryCatalog.ToView(
            item,
            translations,
            overrides,
            counts.GetValueOrDefault(item.Id))).ToList();
        return Result<ListAdminCategoriesResponse>.Success(new ListAdminCategoriesResponse(views, version));
    }
}

public sealed class GetAdminCategoryHandler(IApplicationDbContext db, IClock clock)
    : IHandler<GetAdminCategoryRequest, Result<AdminCategoryView>>
{
    public async Task<Result<AdminCategoryView>> Handle(GetAdminCategoryRequest request, CancellationToken cancellationToken)
    {
        if (await AdminSupport.Allow(db, request.ActorId, StaffPermissionCodes.CategoriesManage, cancellationToken) is { } denied)
        {
            return Result<AdminCategoryView>.Failure(denied);
        }

        var category = await db.ReportCategories.SingleOrDefaultAsync(item => item.Id == request.CategoryId, cancellationToken);
        if (category is null)
        {
            return Result<AdminCategoryView>.Failure(Error.NotFound("category.not_found", "No encontramos esa categoría."));
        }

        var translations = await db.ReportCategoryTranslations.Where(item => item.CategoryId == category.Id).ToListAsync(cancellationToken);
        var overrides = await db.ReportCategoryOverrides.Where(item => item.CategoryId == category.Id).ToListAsync(cancellationToken);
        var counts = await ActiveReportCounts.LoadAsync(db, clock, cancellationToken);
        return Result<AdminCategoryView>.Success(CategoryCatalog.ToView(category, translations, overrides, counts.GetValueOrDefault(category.Id)));
    }
}

internal static class CategoryCatalog
{
    public static AdminCategoryView ToView(
        ReportCategory category,
        IReadOnlyList<ReportCategoryTranslation> translations,
        IReadOnlyList<ReportCategoryOverride> overrides,
        int activeReportCount) =>
        new(
            category.Id,
            category.Code,
            category.Name,
            category.Description,
            category.ButtonIconUrl,
            category.MapIconUrl,
            category.ColorHex,
            category.SortOrder,
            category.IsActive,
            category.TtlMinutes,
            category.ImportanceWeight,
            category.MediaRequirement.ToString(),
            category.IsAdultOnly,
            activeReportCount,
            translations.Where(item => item.CategoryId == category.Id).Select(item => new AdminTranslationView(item.Locale, item.Name, item.Description)).ToArray(),
            overrides.Where(item => item.CategoryId == category.Id).Select(item => new AdminOverrideView(item.CountryId, item.CityId, item.IsEnabled, item.TtlMinutes)).ToArray());
}

internal static class ActiveReportCounts
{
    public static async Task<Dictionary<Guid, int>> LoadAsync(IApplicationDbContext db, IClock clock, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var ids = await db.Reports
            .Where(report => report.Status == ReportStatus.Active && report.ExpiresAt > now)
            .Select(report => report.CategoryId)
            .ToListAsync(cancellationToken);
        return ids.GroupBy(id => id).ToDictionary(group => group.Key, group => group.Count());
    }
}

public sealed record UploadCategoryIconsRequest(Guid ActorId, string? IpAddress, Guid CategoryId, byte[] Button, byte[] Map);

public sealed class UploadCategoryIconsHandler(IApplicationDbContext db, IClock clock, IFileStorage storage, IAdminIconProcessor icons)
    : IHandler<UploadCategoryIconsRequest, Result>
{
    public const int MaxBytes = 1_048_576;

    public async Task<Result> Handle(UploadCategoryIconsRequest request, CancellationToken cancellationToken)
    {
        if (await AdminSupport.Allow(db, request.ActorId, StaffPermissionCodes.CategoriesManage, cancellationToken) is { } denied)
        {
            return Result.Failure(denied);
        }

        var category = await db.ReportCategories.FirstOrDefaultAsync(item => item.Id == request.CategoryId, cancellationToken);
        if (category is null)
        {
            return Result.Failure(Error.NotFound("category.not_found", "No encontramos la categoría."));
        }

        var button = icons.Process(request.Button, MaxBytes, 32, 1024);
        if (button.IsFailure)
        {
            return Result.Failure(button.Error!);
        }

        var map = icons.Process(request.Map, MaxBytes, 32, 1024);
        if (map.IsFailure)
        {
            return Result.Failure(map.Error!);
        }

        var buttonKey = $"public/categories/{category.Id:N}/button.png";
        var mapKey = $"public/categories/{category.Id:N}/map.png";
        await storage.SaveAsync(buttonKey, button.Value!, cancellationToken);
        await storage.SaveAsync(mapKey, map.Value!, cancellationToken);
        var before = new { category.ButtonIconUrl, category.MapIconUrl };
        category.Update(
            category.Name,
            category.Description,
            "/uploads/categories/" + category.Id.ToString("N") + "/button.png",
            "/uploads/categories/" + category.Id.ToString("N") + "/map.png",
            category.ColorHex,
            category.SortOrder,
            category.TtlMinutes,
            category.ImportanceWeight,
            category.MediaRequirement,
            category.ParentCategoryId,
            category.IsAdultOnly);
        var now = clock.UtcNow;
        AdminSupport.Record(db, request.ActorId, "category.icons", "ReportCategory", category.Id.ToString(), AdminSupport.Json(new { before, after = new { category.ButtonIconUrl, category.MapIconUrl } }), request.IpAddress, now);
        await CatalogVersions.BumpAsync(db, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record ListAdminAvatarsRequest(Guid ActorId);

public sealed record AdminAvatarView(Guid Id, string Name, string ImageUrl, int SortOrder, bool IsActive);

public sealed record ListAdminAvatarsResponse(IReadOnlyList<AdminAvatarView> Items);

public sealed class ListAdminAvatarsHandler(IApplicationDbContext db)
    : IHandler<ListAdminAvatarsRequest, Result<ListAdminAvatarsResponse>>
{
    public async Task<Result<ListAdminAvatarsResponse>> Handle(ListAdminAvatarsRequest request, CancellationToken cancellationToken)
    {
        if (await AdminSupport.Allow(db, request.ActorId, StaffPermissionCodes.AvatarsManage, cancellationToken) is { } denied)
        {
            return Result<ListAdminAvatarsResponse>.Failure(denied);
        }

        var rows = await db.AvatarGalleryItems.OrderBy(item => item.SortOrder)
            .Select(item => new AdminAvatarView(item.Id, item.Name, item.ImageUrl, item.SortOrder, item.IsActive))
            .ToListAsync(cancellationToken);
        return Result<ListAdminAvatarsResponse>.Success(new ListAdminAvatarsResponse(rows));
    }
}

public sealed record SaveAdminAvatarRequest(Guid ActorId, string? IpAddress, Guid? AvatarId, string Name, int SortOrder, bool IsActive, byte[]? Image);

public sealed class SaveAdminAvatarValidator : AbstractValidator<SaveAdminAvatarRequest>
{
    public SaveAdminAvatarValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(60);
    }
}

public sealed class SaveAdminAvatarHandler(IApplicationDbContext db, IClock clock, IFileStorage storage, IAvatarImageProcessor images)
    : IHandler<SaveAdminAvatarRequest, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(SaveAdminAvatarRequest request, CancellationToken cancellationToken)
    {
        if (await AdminSupport.Allow(db, request.ActorId, StaffPermissionCodes.AvatarsManage, cancellationToken) is { } denied)
        {
            return Result<Guid>.Failure(denied);
        }

        var now = clock.UtcNow;
        var item = request.AvatarId is { } id
            ? await db.AvatarGalleryItems.FirstOrDefaultAsync(row => row.Id == id, cancellationToken)
            : null;
        if (request.AvatarId is not null && item is null)
        {
            return Result<Guid>.Failure(Error.NotFound("avatar.not_found", "No encontramos el ícono."));
        }

        var created = item is null;
        if (item is null)
        {
            item = AvatarGalleryItem.Create(request.Name.Trim(), "/assets/avatars/10.png", request.SortOrder, now);
            db.AvatarGalleryItems.Add(item);
        }

        var url = item.ImageUrl;
        if (request.Image is { Length: > 0 })
        {
            var processed = images.Process(request.Image, 5 * 1024 * 1024);
            if (processed.IsFailure)
            {
                if (created)
                {
                    db.AvatarGalleryItems.Remove(item);
                }

                return Result<Guid>.Failure(processed.Error!);
            }

            var key = $"public/avatars/{item.Id:N}.jpg";
            await storage.SaveAsync(key, processed.Value!, cancellationToken);
            url = "/uploads/avatars/" + item.Id.ToString("N") + ".jpg";
        }
        else if (created)
        {
            db.AvatarGalleryItems.Remove(item);
            return Result<Guid>.Failure(Error.Validation("avatar.image_required", "Sube la imagen del ícono."));
        }

        var before = created ? null : new { item.Name, item.ImageUrl, item.SortOrder, item.IsActive };
        item.Update(request.Name.Trim(), url, request.SortOrder, request.IsActive, now);

        AdminSupport.Record(db, request.ActorId, created ? "avatar.create" : "avatar.update", nameof(AvatarGalleryItem), item.Id.ToString(), AdminSupport.Json(new { before, after = new { item.Name, item.ImageUrl, item.SortOrder, item.IsActive } }), request.IpAddress, now);
        await db.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(item.Id);
    }
}

public sealed record SetAdminAvatarActiveRequest(Guid ActorId, string? IpAddress, Guid AvatarId, bool IsActive);

public sealed class SetAdminAvatarActiveHandler(IApplicationDbContext db, IClock clock)
    : IHandler<SetAdminAvatarActiveRequest, Result>
{
    public async Task<Result> Handle(SetAdminAvatarActiveRequest request, CancellationToken cancellationToken)
    {
        if (await AdminSupport.Allow(db, request.ActorId, StaffPermissionCodes.AvatarsManage, cancellationToken) is { } denied)
        {
            return Result.Failure(denied);
        }

        var item = await db.AvatarGalleryItems.FirstOrDefaultAsync(row => row.Id == request.AvatarId, cancellationToken);
        if (item is null)
        {
            return Result.Failure(Error.NotFound("avatar.not_found", "No encontramos el ícono."));
        }

        var before = item.IsActive;
        if (request.IsActive)
        {
            item.Activate();
        }
        else
        {
            item.Deactivate();
        }

        var now = clock.UtcNow;
        AdminSupport.Record(db, request.ActorId, "avatar.active", nameof(AvatarGalleryItem), item.Id.ToString(), AdminSupport.Json(new { before, after = item.IsActive }), request.IpAddress, now);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record ListAdminPlansRequest(Guid ActorId);

public sealed record AdminPlanView(Guid Id, string Code, string Name, string Tier, bool IsActive, int? MaxDevices, bool ShowsAds);

public sealed record ListAdminPlansResponse(IReadOnlyList<AdminPlanView> Plans);

public sealed class ListAdminPlansHandler(IApplicationDbContext db)
    : IHandler<ListAdminPlansRequest, Result<ListAdminPlansResponse>>
{
    public async Task<Result<ListAdminPlansResponse>> Handle(ListAdminPlansRequest request, CancellationToken cancellationToken)
    {
        var manage = await StaffGate.HasAsync(db, request.ActorId, StaffPermissionCodes.PlansManage, cancellationToken);
        var view = await StaffGate.HasAsync(db, request.ActorId, StaffPermissionCodes.PlansView, cancellationToken);
        if (request.ActorId == Guid.Empty)
        {
            return Result<ListAdminPlansResponse>.Failure(AdminSupport.Unauthenticated());
        }

        if (!manage && !view)
        {
            return Result<ListAdminPlansResponse>.Failure(AdminSupport.Forbidden());
        }

        var rows = await db.SubscriptionPlans.OrderBy(item => item.SortOrder)
            .Select(item => new AdminPlanView(item.Id, item.Code, item.Name, item.Tier.ToString(), item.IsActive, item.MaxDevices, item.ShowsAds))
            .ToListAsync(cancellationToken);
        return Result<ListAdminPlansResponse>.Success(new ListAdminPlansResponse(rows));
    }
}
