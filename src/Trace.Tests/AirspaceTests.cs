using System.IO;
using Trace.Airspace;
using Trace.Geometry;
using Xunit;
using Xunit.Abstractions;

namespace Trace.Tests;

public class OpenAirReaderTests
{
    private static string DataDir =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data"));

    [Fact]
    public void ParsesCoordinate()
    {
        GeoPoint p = OpenAirReader.ParseCoordinate("57:21:53 N 001:58:35 W");
        Assert.Equal(57.0 + 21.0 / 60.0 + 53.0 / 3600.0, p.Latitude, 6);
        Assert.Equal(-(1.0 + 58.0 / 60.0 + 35.0 / 3600.0), p.Longitude, 6);
    }

    [Theory]
    [InlineData("SFC", 0.0)]
    [InlineData("GND", 0.0)]
    [InlineData("1500 ft", 1500.0)]
    [InlineData("FL115", 11500.0)]
    [InlineData("3000ft AMSL", 3000.0)]
    [InlineData("UNL", 999999.0)]
    public void ParsesAltitude(string text, double expected)
    {
        Assert.Equal(expected, OpenAirReader.ParseAltitude(text), 3);
    }

    [Fact]
    public void ParsesCircle()
    {
        const string oa =
            "AC D\n" +
            "AN TEST CIRCLE\n" +
            "AL SFC\n" +
            "AH 2000 ft\n" +
            "V X=52:00:00 N 001:00:00 W\n" +
            "DC 2\n"; // 2 NM radius

        var reader = new OpenAirReader();
        reader.Parse(new StringReader(oa));

        Assert.Single(reader.Airspaces);
        AirspaceVolume a = reader.Airspaces[0];
        Assert.Equal("D", a.Class);
        Assert.Equal("TEST CIRCLE", a.Name);
        Assert.Equal(0.0, a.LowerLimitFt, 3);
        Assert.Equal(2000.0, a.UpperLimitFt, 3);
        // Centre should be inside; a point 10 km away should be outside.
        Assert.True(Polygon.Contains(a.Boundary, 52.0, -1.0));
        Assert.False(Polygon.Contains(a.Boundary, 52.1, -1.0)); // ~11 km north
    }

    [Fact]
    public void ReadsRealUkAirspaceFile()
    {
        string path = Path.Combine(DataDir, "uk2026-06-11.txt");
        if (!File.Exists(path))
        {
            return;
        }

        var reader = new OpenAirReader();
        reader.Parse(path);

        Assert.True(reader.Airspaces.Count > 500);
        Assert.Contains(reader.Airspaces, a => a.Class == "CTR");
        Assert.Contains(reader.Airspaces, a => a.Name.Contains("ABERDEEN", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MultiFileLoadAppends()
    {
        const string oa =
            "AC D\nAN A\nAL SFC\nAH FL100\nV X=52:00:00 N 001:00:00 W\nDC 1\n";
        var reader = new OpenAirReader();
        reader.Parse(new StringReader(oa));
        reader.Parse(new StringReader(oa));
        Assert.Equal(2, reader.Airspaces.Count);
    }
}

public class AirspaceCheckerTests
{
    private static AirspaceChecker MakeChecker()
    {
        const string oa =
            "AC CTR\nAN ZONE A\nAL SFC\nAH 3000 ft\n" +
            "V X=52:00:00 N 001:00:00 W\nDC 5.4\n" +              // ~10 km radius
            "AC TMA\nAN ZONE B\nAL 5000 ft\nAH FL100\n" +
            "V X=52:00:00 N 001:00:00 W\nDC 5.4\n";                // same place, high base
        var reader = new OpenAirReader();
        reader.Parse(new StringReader(oa));
        return new AirspaceChecker(reader.Airspaces);
    }

    [Fact]
    public void PointInsideLowZoneAtLowAltitudeInfringes()
    {
        AirspaceChecker c = MakeChecker();
        AirspaceVolume? hit = c.PointInAirspace(52.0, -1.0, altitudeFt: 1000);
        Assert.NotNull(hit);
        Assert.Equal("ZONE A", hit!.Name);
    }

    [Fact]
    public void PointBelowZoneBaseDoesNotInfringeThatZone()
    {
        AirspaceChecker c = MakeChecker();
        // At 1000 ft, only ZONE A (SFC-3000) applies, not ZONE B (5000+).
        var classes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "TMA" };
        Assert.Null(c.PointInAirspace(52.0, -1.0, 1000, classes));
        // Above 5000 ft ZONE B applies.
        Assert.NotNull(c.PointInAirspace(52.0, -1.0, 6000, classes));
    }

    [Fact]
    public void PointOutsideAllZones()
    {
        AirspaceChecker c = MakeChecker();
        Assert.False(c.IsInfringing(53.0, -1.0, 1000)); // ~111 km north
    }

    [Fact]
    public void ClassFilterRestrictsMatches()
    {
        AirspaceChecker c = MakeChecker();
        var onlyCtr = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CTR" };
        Assert.NotNull(c.PointInAirspace(52.0, -1.0, 1000, onlyCtr));
        var onlyDanger = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "D" };
        Assert.Null(c.PointInAirspace(52.0, -1.0, 1000, onlyDanger));
    }

    [Fact]
    public void ZoneIntersectionDetectsBarrelClippingAirspace()
    {
        AirspaceChecker c = MakeChecker();
        // A barrel centred ~12 km from the 10 km-radius CTR with a 3 km radius
        // should clip it; a 1 km barrel at the same place should not.
        var ctr = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CTR" };
        // Point ~12 km north of centre (52.108 ≈ +12 km).
        Assert.NotEmpty(c.ZoneIntersections(52.108, -1.0, radiusKm: 3.0, zoneCeilingFt: 5000, ctr));
        Assert.Empty(c.ZoneIntersections(52.108, -1.0, radiusKm: 1.0, zoneCeilingFt: 5000, ctr));
    }

    [Fact]
    public void ZoneIntersectionIgnoresAirspaceAboveCeiling()
    {
        AirspaceChecker c = MakeChecker();
        var tma = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "TMA" };
        // ZONE B base is 5000 ft; a barrel reaching only 3000 ft must ignore it.
        Assert.Empty(c.ZoneIntersections(52.0, -1.0, 3.0, zoneCeilingFt: 3000, tma));
        Assert.NotEmpty(c.ZoneIntersections(52.0, -1.0, 3.0, zoneCeilingFt: 8000, tma));
    }
}
