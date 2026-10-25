using System.Text.Json;
using Situ.Application.Common.Results;

namespace Situ.Application.Features.Configuration;

public static class PanelCopies
{
    public sealed record Item(string CountryCode, string Locale, string Browse, string Report);

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static IReadOnlyList<Item> Read(string? json)
    {
        var parsed = Parse(json);
        return parsed.IsSuccess ? parsed.Value! : [];
    }

    public static Result<IReadOnlyList<Item>> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Result<IReadOnlyList<Item>>.Success([]);
        }

        List<Row>? rows;
        try
        {
            rows = JsonSerializer.Deserialize<List<Row>>(json, Options);
        }
        catch (JsonException)
        {
            return Result<IReadOnlyList<Item>>.Failure(Error.Validation("settings.invalid_value", "El texto del panel no tiene un JSON válido."));
        }

        if (rows is null || rows.Count > 50)
        {
            return Result<IReadOnlyList<Item>>.Failure(Error.Validation("settings.invalid_value", "El texto del panel admite hasta 50 variantes."));
        }

        var items = new List<Item>(rows.Count);
        foreach (var row in rows)
        {
            var country = row.CountryCode?.Trim().ToUpperInvariant() ?? "";
            var locale = row.Locale?.Trim() ?? "";
            var browse = row.Browse?.Trim() ?? "";
            var report = row.Report?.Trim() ?? "";
            if (country is not "*" && (country.Length != 2 || country.Any(character => character is < 'A' or > 'Z')))
            {
                return Result<IReadOnlyList<Item>>.Failure(Error.Validation("settings.invalid_value", "El país del texto del panel es de dos letras, o * para el valor general."));
            }

            if (locale.Length is < 2 or > 15 || browse.Length is < 1 or > 80 || report.Length is < 1 or > 80)
            {
                return Result<IReadOnlyList<Item>>.Failure(Error.Validation("settings.invalid_value", "Revisa el idioma y los textos del panel."));
            }

            items.Add(new Item(country, locale, browse, report));
        }

        return Result<IReadOnlyList<Item>>.Success(items);
    }

    private sealed record Row(string? CountryCode, string? Locale, string? Browse, string? Report);
}
