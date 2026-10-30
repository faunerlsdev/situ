using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Situ.Application.Common.Abstractions;
using Situ.Application.Common.Results;
using Situ.Application.Features.Profiles;
using Situ.Domain.Administration;

namespace Situ.Application.Features.Moderation;

/// <summary>
/// Rejects insults, sexual content, threats and third-party personal data
/// before a publication or comment is stored. There is no direct-message write path yet.
/// </summary>
public static partial class ContentFilter
{
    public const string Message =
        "Ese texto no se puede publicar. Quita insultos, contenido sexual, amenazas o datos personales de otras personas.";

    public static Error Rejected(string field) =>
        Error.Field("content.not_allowed", field, Message);

    public static string? Reason(string? text, IReadOnlySet<string> terms)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var folded = Fold(text);
        if (terms.Any(term => term.Length >= 3 && folded.Contains(term, StringComparison.Ordinal)))
        {
            return "term";
        }

        if (PhonePattern().IsMatch(text) || CompactNumberPattern().IsMatch(text))
        {
            return "phone";
        }

        if (DottedIdPattern().IsMatch(text) || NationalIdKeywordPattern().IsMatch(folded))
        {
            return "national_id";
        }

        if (AddressPattern().IsMatch(folded))
        {
            return "address";
        }

        return null;
    }

    public static bool Rejects(string? text, IReadOnlySet<string> terms) => Reason(text, terms) is not null;

    public static string Fold(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    [GeneratedRegex(@"(?<!\d)(?:\+?\d{1,3}[\s.\-])?\d{3}[\s.\-]\d{3}[\s.\-]\d{4}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"\b\d{8,10}\b", RegexOptions.CultureInvariant)]
    private static partial Regex CompactNumberPattern();

    [GeneratedRegex(@"\b\d{1,3}(?:\.\d{3}){3}\b", RegexOptions.CultureInvariant)]
    private static partial Regex DottedIdPattern();

    [GeneratedRegex(@"\b(?:cedula|cc|nit)\s*[:#]?\s*\d[\d.\s]{5,16}\d", RegexOptions.CultureInvariant)]
    private static partial Regex NationalIdKeywordPattern();

    [GeneratedRegex(@"\b(?:calle|carrera|cra|kr|avenida|av|diagonal|diag|transversal|trans|tv|apto|apartamento|manzana)\.?\s+\d{1,4}\b", RegexOptions.CultureInvariant)]
    private static partial Regex AddressPattern();
}

public static class ContentFilterCatalog
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, (DateTime Stamp, IReadOnlySet<string> Terms)> Cache = new(StringComparer.Ordinal);

    public static async Task<IReadOnlySet<string>> TermsAsync(IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var settings = await ProfileRules.SettingsAsync(db, cancellationToken);
        return Merge(Current(), ProfileRules.List(settings, SystemSettingKeys.ProfileBlockedTerms));
    }

    public static IReadOnlySet<string> Current()
    {
        var path = ResolvePath();
        return path is null ? Empty() : Read(path);
    }

    public static IReadOnlySet<string> Read(string path)
    {
        var full = Path.GetFullPath(path);
        var stamp = File.Exists(full) ? File.GetLastWriteTimeUtc(full) : DateTime.MinValue;
        lock (Gate)
        {
            if (Cache.TryGetValue(full, out var hit) && hit.Stamp == stamp)
            {
                return hit.Terms;
            }

            var terms = Parse(File.Exists(full) ? File.ReadAllText(full) : "");
            Cache[full] = (stamp, terms);
            return terms;
        }
    }

    public static IReadOnlySet<string> Merge(IReadOnlySet<string> fromFile, IReadOnlySet<string> fromSettings)
    {
        if (fromFile.Count == 0)
        {
            return fromSettings;
        }

        if (fromSettings.Count == 0)
        {
            return fromFile;
        }

        var merged = new HashSet<string>(fromFile, StringComparer.Ordinal);
        foreach (var term in fromSettings)
        {
            var folded = ContentFilter.Fold(term);
            if (folded.Length >= 3)
            {
                merged.Add(folded);
            }
        }

        return merged;
    }

    public static IReadOnlySet<string> Parse(string raw)
    {
        var terms = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in raw.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            var folded = ContentFilter.Fold(trimmed);
            if (folded.Length >= 3)
            {
                terms.Add(folded);
            }
        }

        return terms;
    }

    private static string? ResolvePath()
    {
        var overridePath = Environment.GetEnvironmentVariable("SITU_CONTENT_FILTER_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return overridePath;
        }

        var besideApp = Path.Combine(AppContext.BaseDirectory, "config", "content-filter.txt");
        return File.Exists(besideApp) ? besideApp : null;
    }

    private static IReadOnlySet<string> Empty() => new HashSet<string>(StringComparer.Ordinal);
}
