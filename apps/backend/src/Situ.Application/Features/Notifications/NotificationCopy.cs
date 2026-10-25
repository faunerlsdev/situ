namespace Situ.Application.Features.Notifications;

/// <summary>Short es-CO copy. No names, no report description, no coordinates.</summary>
internal static class NotificationCopy
{
    public static (string Title, string Body) Nearby(string category, double meters) =>
        ("Cerca de ti", $"{Clip(category)} a {Distance(meters)}");

    public static (string Title, string Body) City(string city, string category) =>
        ("En tu ciudad", $"Nuevo reporte en {Clip(city)}: {Clip(category)}");

    public static (string Title, string Body) Friend(string category) =>
        ("Un amigo", $"Un amigo reportó: {Clip(category)}");

    public static (string Title, string Body) Comment() =>
        ("Tu reporte", "Alguien comentó tu reporte.");

    public static (string Title, string Body) Digest(int count) =>
        ("En tu zona", $"Hay {count} reportes nuevos en tu zona");

    public static string Distance(double meters)
    {
        if (meters < 1000)
        {
            return $"{Math.Max(1, (int)Math.Round(meters))} m";
        }

        return $"{meters / 1000d:0.#} km";
    }

    private static string Clip(string value)
    {
        var text = value.Trim();
        return text.Length <= 40 ? text : text[..40];
    }
}
