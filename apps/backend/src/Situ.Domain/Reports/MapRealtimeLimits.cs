using System.Globalization;

namespace Situ.Domain.Reports;

/// <summary>Bounds for the map hub. Settings may only tighten them.</summary>
public static class MapRealtimeLimits
{
    public const double DefaultDegrees = 0.05;
    public const int DefaultMaxCells = 16;
    public const int HardMaxCells = 16;
    public const int DefaultSubscribeCallsPerMinute = 20;
    public const int HardSubscribeCallsPerMinute = 20;
    public const int DefaultCoalesceMilliseconds = 1000;

    public static double Degrees(string? raw)
    {
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return DefaultDegrees;
        }

        return value is >= 0.01 and <= 1 ? value : DefaultDegrees;
    }

    public static int MaxCells(string? raw) => Within(raw, DefaultMaxCells, 1, HardMaxCells);

    public static int SubscribeCallsPerMinute(string? raw) =>
        Within(raw, DefaultSubscribeCallsPerMinute, 1, HardSubscribeCallsPerMinute);

    public static int CoalesceMilliseconds(string? raw)
    {
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return DefaultCoalesceMilliseconds;
        }

        return Math.Clamp(value, 200, 10_000);
    }

    private static int Within(string? raw, int fallback, int min, int max)
    {
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < min || value > max)
        {
            return fallback;
        }

        return value;
    }
}
