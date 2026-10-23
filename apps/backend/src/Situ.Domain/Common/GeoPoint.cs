namespace Situ.Domain.Common;

/// <summary>WGS84 coordinate. Infrastructure stores it as a PostGIS geography(Point, 4326).</summary>
public readonly record struct GeoPoint
{
    private const double EarthRadiusMeters = 6_371_000d;

    public GeoPoint(double latitude, double longitude)
    {
        if (double.IsNaN(latitude) || latitude < -90 || latitude > 90)
        {
            throw new DomainException("geo.invalid_latitude", "Latitude must be between -90 and 90.");
        }

        if (double.IsNaN(longitude) || longitude < -180 || longitude > 180)
        {
            throw new DomainException("geo.invalid_longitude", "Longitude must be between -180 and 180.");
        }

        Latitude = latitude;
        Longitude = longitude;
    }

    public double Latitude { get; }
    public double Longitude { get; }

    /// <summary>Great-circle distance (haversine) in meters.</summary>
    public double DistanceInMetersTo(GeoPoint other)
    {
        var lat1 = ToRadians(Latitude);
        var lat2 = ToRadians(other.Latitude);
        var dLat = lat2 - lat1;
        var dLon = ToRadians(other.Longitude - Longitude);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return EarthRadiusMeters * c;
    }

    /// <summary>True when <paramref name="other"/> is at most <paramref name="meters"/> away (e.g. the 1 km notification rule).</summary>
    public bool IsWithin(GeoPoint other, double meters) => DistanceInMetersTo(other) <= meters;

    private static double ToRadians(double degrees) => degrees * Math.PI / 180d;
}
