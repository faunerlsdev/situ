namespace Situ.Application.Features.Administration.Catalog;

/// <summary>
/// The 23 category drawings. Seed codes that predate these slugs map onto one of them.
/// </summary>
public static class CategoryIconFiles
{
    public static readonly string[] Slugs =
    [
        "accidente",
        "trafico",
        "vehiculo_varado",
        "hurto",
        "puesto_control",
        "basuras",
        "hueco",
        "derrumbe",
        "inundacion",
        "incendio",
        "protesta",
        "obra_via",
        "corte_luz",
        "corte_agua",
        "peligro",
        "policia",
        "emergencia",
        "animal",
        "evento",
        "ruido",
        "semaforo",
        "via_cerrada",
        "otro",
    ];

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal)
    {
        ["accidente_transito"] = "accidente",
        ["manifestacion"] = "protesta",
        ["obra_vial"] = "obra_via",
        ["corte_servicio"] = "corte_luz",
    };

    public static string? SlugFor(string code)
    {
        if (Aliases.TryGetValue(code, out var alias))
        {
            return alias;
        }

        return Array.IndexOf(Slugs, code) >= 0 ? code : null;
    }

    public static string ButtonPath(string slug) => $"/assets/categories/button/{slug}.png";

    public static string MapPath(string slug) => $"/assets/categories/map/{slug}.png";
}
