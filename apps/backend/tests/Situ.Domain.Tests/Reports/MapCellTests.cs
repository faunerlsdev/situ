using Situ.Domain.Reports;

namespace Situ.Domain.Tests.Reports;

public class MapCellTests
{
    private const double Degrees = 0.05;

    [Theory]
    [InlineData(0, 0, "0:0")]
    [InlineData(0.05, 0.05, "1:1")]
    [InlineData(-0.0001, 0, "0:-1")]
    [InlineData(0, -0.05, "-1:0")]
    [InlineData(4.711, -74.072, "-1482:94")]
    [InlineData(90, 180, "3600:1800")]
    [InlineData(-90, -180, "-3600:-1800")]
    public void Id_quantizes_edges_and_both_hemispheres(double latitude, double longitude, string expected)
    {
        Assert.Equal(expected, MapCell.Id(latitude, longitude, Degrees));
        Assert.True(MapCell.IsValid(expected, Degrees));
    }

    [Theory]
    [InlineData("01:2")]
    [InlineData("+1:2")]
    [InlineData("1:2:3")]
    [InlineData("1:")]
    [InlineData(":1")]
    [InlineData("")]
    [InlineData("1:2 ")]
    [InlineData("4000:0")]
    [InlineData("0:2000")]
    public void Invalid_ids_are_rejected(string cellId)
    {
        Assert.False(MapCell.IsValid(cellId, Degrees));
    }

    [Fact]
    public void Cover_stops_when_the_viewport_is_wider_than_the_cap_or_crosses_the_antimeridian()
    {
        Assert.True(MapCell.TryCover(4.70, -74.08, 4.74, -74.04, Degrees, 16, out var cells));
        Assert.Equal(new[] { "-1482:94", "-1481:94" }, cells);

        Assert.False(MapCell.TryCover(0, -1, 1, 1, Degrees, 16, out _));
        Assert.False(MapCell.TryCover(0, 170, 0, -170, Degrees, 16, out _));
        Assert.False(MapCell.TryCover(91, 0, 91, 0, Degrees, 16, out _));
    }

    [Fact]
    public void Limits_fall_back_when_the_setting_is_missing_or_too_loose()
    {
        Assert.Equal(0.05, MapRealtimeLimits.Degrees(null));
        Assert.Equal(0.05, MapRealtimeLimits.Degrees("9"));
        Assert.Equal(0.1, MapRealtimeLimits.Degrees("0.1"));
        Assert.Equal(16, MapRealtimeLimits.MaxCells("100"));
        Assert.Equal(20, MapRealtimeLimits.SubscribeCallsPerMinute("0"));
        Assert.Equal(1000, MapRealtimeLimits.CoalesceMilliseconds("no"));
        Assert.Equal(200, MapRealtimeLimits.CoalesceMilliseconds("1"));
    }
}
