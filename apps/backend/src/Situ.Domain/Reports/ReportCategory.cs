using System.Text.RegularExpressions;
using Situ.Domain.Common;

namespace Situ.Domain.Reports;

/// <summary>
/// Admin-managed report type shown as a button in the bottom panel (Accidente de tránsito, Hurto, Retén, Basuras...).
/// Stored in the database so it can change without releasing the app (RF-CAT-01..04).
/// </summary>
public sealed class ReportCategory : AggregateRoot
{
    private static readonly Regex CodePattern = new(@"^[a-z0-9_]{2,40}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ColorPattern = new(@"^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private ReportCategory()
    {
        Code = null!;
        Name = null!;
        Description = null!;
        ButtonIconUrl = null!;
        MapIconUrl = null!;
        ColorHex = null!;
    }

    /// <summary>Stable key used by clients and the algorithm (e.g. "traffic_accident").</summary>
    public string Code { get; private set; }

    public string Name { get; private set; }
    public string Description { get; private set; }
    public string ButtonIconUrl { get; private set; }
    public string MapIconUrl { get; private set; }
    public string ColorHex { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>How long a report of this category stays on the map.</summary>
    public int TtlMinutes { get; private set; }

    /// <summary>Weight (0-10) used by the important-news algorithm.</summary>
    public double ImportanceWeight { get; private set; }

    public MediaRequirement MediaRequirement { get; private set; }
    public Guid? ParentCategoryId { get; private set; }

    /// <summary>Hidden from users under 18 (RF-MIN-06).</summary>
    public bool IsAdultOnly { get; private set; }

    public static ReportCategory Create(
        string code,
        string name,
        string description,
        string buttonIconUrl,
        string mapIconUrl,
        string colorHex,
        int sortOrder,
        int ttlMinutes,
        double importanceWeight,
        MediaRequirement mediaRequirement = MediaRequirement.None,
        Guid? parentCategoryId = null,
        bool isAdultOnly = false)
    {
        var normalizedCode = Guard.NotEmpty(code, nameof(code), 40).ToLowerInvariant();
        Guard.That(CodePattern.IsMatch(normalizedCode), "report_category.invalid_code", "Code must be lower-case letters, digits or '_'.");

        var category = new ReportCategory
        {
            Code = normalizedCode,
            IsActive = true,
        };
        category.Apply(name, description, buttonIconUrl, mapIconUrl, colorHex, sortOrder, ttlMinutes, importanceWeight, mediaRequirement, parentCategoryId, isAdultOnly);
        return category;
    }

    public void Update(
        string name,
        string description,
        string buttonIconUrl,
        string mapIconUrl,
        string colorHex,
        int sortOrder,
        int ttlMinutes,
        double importanceWeight,
        MediaRequirement mediaRequirement,
        Guid? parentCategoryId,
        bool isAdultOnly) =>
        Apply(name, description, buttonIconUrl, mapIconUrl, colorHex, sortOrder, ttlMinutes, importanceWeight, mediaRequirement, parentCategoryId, isAdultOnly);

    public void ReplaceIcons(string buttonIconUrl, string mapIconUrl, DateTimeOffset now)
    {
        ButtonIconUrl = Guard.NotEmpty(buttonIconUrl, nameof(buttonIconUrl), 500);
        MapIconUrl = Guard.NotEmpty(mapIconUrl, nameof(mapIconUrl), 500);
        MarkUpdated(now);
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    /// <summary>TTL after applying a country/city override, if any.</summary>
    public TimeSpan EffectiveLifetime(int? overrideTtlMinutes = null) =>
        TimeSpan.FromMinutes(overrideTtlMinutes ?? TtlMinutes);

    private void Apply(
        string name,
        string description,
        string buttonIconUrl,
        string mapIconUrl,
        string colorHex,
        int sortOrder,
        int ttlMinutes,
        double importanceWeight,
        MediaRequirement mediaRequirement,
        Guid? parentCategoryId,
        bool isAdultOnly)
    {
        Name = Guard.NotEmpty(name, nameof(name), 60);
        Description = Guard.NotEmpty(description, nameof(description), 160);
        ButtonIconUrl = Guard.NotEmpty(buttonIconUrl, nameof(buttonIconUrl), 500);
        MapIconUrl = Guard.NotEmpty(mapIconUrl, nameof(mapIconUrl), 500);

        var color = Guard.NotEmpty(colorHex, nameof(colorHex), 7);
        Guard.That(ColorPattern.IsMatch(color), "report_category.invalid_color", "Color must look like #RRGGBB.");
        ColorHex = color.ToUpperInvariant();

        SortOrder = sortOrder;
        TtlMinutes = Guard.InRange(ttlMinutes, 5, 60 * 24 * 30, nameof(ttlMinutes));
        ImportanceWeight = Guard.InRange(importanceWeight, 0d, 10d, nameof(importanceWeight));
        MediaRequirement = mediaRequirement;
        ParentCategoryId = parentCategoryId;
        IsAdultOnly = isAdultOnly;
    }
}
