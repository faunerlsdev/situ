using Situ.Domain.Common;

namespace Situ.Domain.Profiles;

/// <summary>Rounds a position onto a grid so the stored and delivered point is never the raw reading.</summary>
public static class LocationGrid
{
    public static GeoPoint Round(GeoPoint point, int meters)
    {
        var step = Math.Max(1, meters) / 111_320d;
        var latitude = Math.Round(point.Latitude / step) * step;
        var lonStep = step / Math.Max(0.2, Math.Cos(point.Latitude * Math.PI / 180d));
        var longitude = Math.Round(point.Longitude / lonStep) * lonStep;
        longitude = Math.Clamp(longitude, -180d, 180d);
        latitude = Math.Clamp(latitude, -90d, 90d);
        return new GeoPoint(latitude, longitude);
    }

    public static DateTimeOffset RoundTime(DateTimeOffset instant, int minutes)
    {
        var size = TimeSpan.FromMinutes(Math.Max(1, minutes)).Ticks;
        var rounded = instant.UtcTicks - (instant.UtcTicks % size);
        return new DateTimeOffset(rounded, TimeSpan.Zero);
    }
}
