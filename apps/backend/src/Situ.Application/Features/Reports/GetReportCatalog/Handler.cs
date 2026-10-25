using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Domain.Guardianship;
using Situ.Domain.Identity;
using Situ.Domain.Reports;

namespace Situ.Application.Features.Reports.GetReportCatalog;

public sealed record GetReportCatalogRequest(Guid? UserId, string? AcceptLanguage);

public sealed record ReportCategoryNode(
    Guid Id,
    Guid? ParentCategoryId,
    string Code,
    string Name,
    string Description,
    string ButtonIconUrl,
    string MapIconUrl,
    string ColorHex,
    int SortOrder,
    int TtlMinutes,
    double ImportanceWeight,
    string MediaRequirement,
    bool IsAdultOnly);

public sealed record GetReportCatalogResponse(
    int CatalogVersion,
    string Locale,
    bool AdultCatalog,
    Guid? CityId,
    IReadOnlyList<ReportCategoryNode> Categories);

public sealed class GetReportCatalogValidator : AbstractValidator<GetReportCatalogRequest>
{
    public GetReportCatalogValidator()
    {
        RuleFor(request => request.AcceptLanguage).MaximumLength(200);
    }
}

public sealed class GetReportCatalogHandler(IApplicationDbContext db, IClock clock)
    : IHandler<GetReportCatalogRequest, Result<GetReportCatalogResponse>>
{
    public async Task<Result<GetReportCatalogResponse>> Handle(
        GetReportCatalogRequest request,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        User? user = null;
        if (request.UserId is { } userId)
        {
            user = await db.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        }

        var adult = user is not null && MinorProtectionPolicy.CanUseAdultOnlyCategories(user.AgeOn(now));
        var cityId = user?.CityId;
        Guid? countryId = user?.CountryId;
        var locale = ResolveLocale(request.AcceptLanguage, user?.Locale);

        var categories = await db.ReportCategories.Where(category => category.IsActive).ToListAsync(cancellationToken);
        var overrides = await db.ReportCategoryOverrides.ToListAsync(cancellationToken);
        var translations = await db.ReportCategoryTranslations
            .Where(translation => translation.Locale == locale || translation.Locale == "es-CO" || translation.Locale == "es")
            .ToListAsync(cancellationToken);

        var nodes = new List<ReportCategoryNode>();
        foreach (var category in categories.OrderBy(category => category.SortOrder).ThenBy(category => category.Name))
        {
            if (category.IsAdultOnly && !adult)
            {
                continue;
            }

            var cityOverride = overrides.FirstOrDefault(item => item.CategoryId == category.Id && item.CityId == cityId && cityId is not null);
            var countryOverride = overrides.FirstOrDefault(item => item.CategoryId == category.Id && item.CountryId == countryId && item.CityId is null);
            var chosen = cityOverride ?? countryOverride;
            if (chosen is { IsEnabled: false })
            {
                continue;
            }

            var translation = translations.FirstOrDefault(item => item.CategoryId == category.Id && item.Locale == locale)
                ?? translations.FirstOrDefault(item => item.CategoryId == category.Id && item.Locale == "es-CO")
                ?? translations.FirstOrDefault(item => item.CategoryId == category.Id && item.Locale == "es");
            var ttl = (int)category.EffectiveLifetime(chosen?.TtlMinutes).TotalMinutes;
            nodes.Add(new ReportCategoryNode(
                category.Id,
                category.ParentCategoryId,
                category.Code,
                translation?.Name ?? category.Name,
                translation?.Description ?? category.Description,
                category.ButtonIconUrl,
                category.MapIconUrl,
                category.ColorHex,
                category.SortOrder,
                ttl,
                category.ImportanceWeight,
                category.MediaRequirement.ToString(),
                category.IsAdultOnly));
        }

        var version = await CatalogVersions.ReadAsync(db, cancellationToken);
        return Result<GetReportCatalogResponse>.Success(new GetReportCatalogResponse(version, locale, adult, cityId, nodes));
    }

    internal static string ResolveLocale(string? acceptLanguage, string? userLocale)
    {
        var header = acceptLanguage?.Split(',').FirstOrDefault()?.Split(';').FirstOrDefault()?.Trim();
        var chosen = string.IsNullOrWhiteSpace(header) ? userLocale : header;
        if (string.IsNullOrWhiteSpace(chosen))
        {
            return "es-CO";
        }

        return chosen.StartsWith("es", StringComparison.OrdinalIgnoreCase) ? "es-CO" : chosen;
    }
}
