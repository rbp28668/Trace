using Trace.Geometry;
using Xunit;

namespace Trace.Tests;

public class GeodesyTests
{
    // Land's End (50.06639°N 5.71472°W) to John o' Groats (58.64389°N 3.07000°W).
    private const double LandsEndLat = 50.06639;
    private const double LandsEndLon = -5.71472;
    private const double JogLat = 58.64389;
    private const double JogLon = -3.07000;

    [Fact]
    public void DistanceMatchesKnownGeodesic()
    {
        double d = Geodesy.DistanceKm(LandsEndLat, LandsEndLon, JogLat, JogLon);
        // WGS84 (Vincenty) geodesic distance ≈ 969.93 km.
        Assert.Equal(969.93, d, 1); // within ~0.05 km
    }

    [Fact]
    public void InitialBearingMatchesKnownGeodesic()
    {
        double b = Geodesy.BearingDegrees(LandsEndLat, LandsEndLon, JogLat, JogLon);
        // WGS84 initial bearing Land's End -> John o' Groats ≈ 9.14°.
        Assert.Equal(9.14, b, 1);
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

    // DAY1 (RacerDay1TaskA) legs, from the published task sheet's Dist column
    // (see docs/task_sheets/RacerDay1TaskA_barrel_table.png). WGS84 reproduces
    // each to 0.1 km; the old spherical model was ~0.3% short.
    [Theory]
    [InlineData(52.186317, -0.111233, 52.072583, -1.313067, 83.3)] // Gransden -> Banbury
    [InlineData(52.072583, -1.313067, 52.324300, -0.598417, 56.3)] // Banbury -> Rushden
    [InlineData(52.324300, -0.598417, 52.185833, -0.895317, 25.5)] // Rushden -> Northampton S
    [InlineData(52.185833, -0.895317, 52.220267, 0.000367, 61.4)]  // Northampton S -> Hardwick
    [InlineData(52.220267, 0.000367, 52.186317, -0.111233, 8.5)]   // Hardwick -> Gransden
    public void Day1LegsMatchTaskSheet(double lat1, double lon1, double lat2, double lon2, double expectedKm)
    {
        Assert.Equal(expectedKm, Geodesy.DistanceKm(lat1, lon1, lat2, lon2), 1);
    }
}
