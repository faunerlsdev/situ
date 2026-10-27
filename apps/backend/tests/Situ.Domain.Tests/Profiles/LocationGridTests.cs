using Situ.Domain.Common;
using Situ.Domain.Profiles;

namespace Situ.Domain.Tests.Profiles;

public class LocationGridTests
{
    [Fact]
    public void Rounding_is_stable_and_never_finer_than_the_requested_grid()
    {
        var raw = new GeoPoint(4.711234, -74.072345);
        var friends = LocationGrid.Round(raw, 25);
        var general = LocationGrid.Round(friends, 300);

        Assert.True(raw.DistanceInMetersTo(friends) <= 40);
        Assert.True(friends.DistanceInMetersTo(general) <= 500);
    }

    [Fact]
    public void Last_seen_is_rounded_down_to_the_configured_bucket()
    {
        var instant = new DateTimeOffset(2026, 10, 27, 14, 7, 40, TimeSpan.Zero);
        Assert.Equal(new DateTimeOffset(2026, 10, 27, 14, 5, 0, TimeSpan.Zero), LocationGrid.RoundTime(instant, 5));
    }
}
