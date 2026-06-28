using Trace.Geometry;
using Xunit;

namespace Trace.Tests;

public class GeodesyTests
{
    // Reference values from the well-known Movable Type great-circle pages:
    // 50.0359°N 5.4253°W (Land's End) to 58.6440°N 3.0700°W (John o' Groats).
    private const double LandsEndLat = 50.06639;
    private const double LandsEndLon = -5.71472;
    private const double JogLat = 58.64389;
    private const double JogLon = -3.07000;

    [Fact]
    public void DistanceMatchesKnownGreatCircle()
    {
        double d = Geodesy.DistanceKm(LandsEndLat, LandsEndLon, JogLat, JogLon);
        // Published distance ≈ 968.9 km.
        Assert.Equal(968.9, d, 0); // within ~0.5 km
    }

    [Fact]
    public void DistanceIsSymmetric()
    {
        double ab = Geodesy.DistanceKm(LandsEndLat, LandsEndLon, JogLat, JogLon);
        double ba = Geodesy.DistanceKm(JogLat, JogLon, LandsEndLat, LandsEndLon);
        Assert.Equal(ab, ba, 6);
    }

    [Fact]
    public void DueNorthBearingIsZero()
    {
        double b = Geodesy.BearingDegrees(50.0, -1.0, 51.0, -1.0);
        Assert.Equal(0.0, b, 3);
    }

    [Fact]
    public void DueEastBearingIsNinety()
    {
        // Near the equator a small step east is ~90°.
        double b = Geodesy.BearingDegrees(0.0, 0.0, 0.0, 1.0);
        Assert.Equal(90.0, b, 3);
    }

    [Fact]
    public void PointAtRoundTripsWithDistanceAndBearing()
    {
        double bearing = 73.0;
        double dist = 42.0;
        (double lat, double lon) = Geodesy.PointAt(52.0, -1.0, bearing, dist);

        Assert.Equal(dist, Geodesy.DistanceKm(52.0, -1.0, lat, lon), 3);
        Assert.Equal(bearing, Geodesy.BearingDegrees(52.0, -1.0, lat, lon), 2);
    }

    [Theory]
    [InlineData(-10.0, 350.0)]
    [InlineData(370.0, 10.0)]
    [InlineData(360.0, 0.0)]
    public void Normalize360Wraps(double input, double expected)
    {
        Assert.Equal(expected, Geodesy.Normalize360(input), 6);
    }
}
