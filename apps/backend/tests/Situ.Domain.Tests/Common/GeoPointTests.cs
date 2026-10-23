using Situ.Domain.Common;

namespace Situ.Domain.Tests.Common;

public class GeoPointTests
{
    private const double NotificationRadiusMeters = 1_000;
    private const double EarthRadiusMeters = 6_371_000d;

    [Fact]
    public void IsWithin_uses_the_1_km_notification_radius()
    {
        var origin = new GeoPoint(4.7110, -74.0721);
        var boundary = MoveNorth(origin, NotificationRadiusMeters);
        var boundaryDistance = origin.DistanceInMetersTo(boundary);

        Assert.Equal(0, origin.DistanceInMetersTo(origin), 0);
        Assert.InRange(boundaryDistance, 999, 1_001);
        Assert.True(origin.IsWithin(origin, NotificationRadiusMeters));
        Assert.True(origin.IsWithin(MoveNorth(origin, 500), NotificationRadiusMeters));
        Assert.Equal(boundaryDistance <= NotificationRadiusMeters, origin.IsWithin(boundary, NotificationRadiusMeters));
        Assert.False(origin.IsWithin(MoveNorth(origin, 1_001), NotificationRadiusMeters));
        Assert.False(origin.IsWithin(MoveNorth(origin, 1_500), NotificationRadiusMeters));
    }

    private static GeoPoint MoveNorth(GeoPoint origin, double meters)
    {
        var degrees = meters / EarthRadiusMeters * (180d / Math.PI);
        return new GeoPoint(origin.Latitude + degrees, origin.Longitude);
    }
}
