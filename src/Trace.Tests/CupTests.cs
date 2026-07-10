using System.IO;
using Trace.Io;
using Trace.Model;
using Xunit;

namespace Trace.Tests;

public class CupTests
{
    // data/ lives four levels up from bin/Debug/net10.0.
    private static string DataDir =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data"));

    [Fact]
    public void ParsesLatitudeAndLongitude()
    {
        // 5107.830N = 51° 07.830' N; 01410.467E = 14° 10.467' E.
        Assert.Equal(51.0 + 7.830 / 60.0, CupReader.ParseLatitude("5107.830N"), 6);
        Assert.Equal(14.0 + 10.467 / 60.0, CupReader.ParseLongitude("01410.467E"), 6);
        // West is negative.
        Assert.Equal(-(2.0 + 23.525 / 60.0), CupReader.ParseLongitude("00223.525W"), 6);
    }

    [Fact]
    public void LatLonFormatRoundTrips()
    {
        double lat = 52.0 + 10.970 / 60.0;
        double lon = -(2.0 + 23.525 / 60.0);
        Assert.Equal(lat, CupReader.ParseLatitude(CupWriter.FormatLatitude(lat)), 5);
        Assert.Equal(lon, CupReader.ParseLongitude(CupWriter.FormatLongitude(lon)), 5);
    }

    [Fact]
    public void SplitCsvHonoursQuotedCommas()
    {
        List<string> f = CupReader.SplitCsv("\"Abbey St Bathans\",ABA,UK,5551.267N,00223.525W,144m,1,,,,,\"Turn Point, River Junction\",,");
        Assert.Equal("Abbey St Bathans", f[0]);
        Assert.Equal("ABA", f[1]);
        Assert.Equal("Turn Point, River Junction", f[11]);
    }

    [Fact]
    public void ReadsRealBgaWaypointFile()
    {
        string path = Path.Combine(DataDir, "BGA TPs 2026-06-10.cup");
        Assert.True(File.Exists(path), $"missing test data: {path}");

        var reader = new CupReader();
        reader.Parse(path);

        Assert.True(reader.Waypoints.Count > 1000);
        Waypoint first = reader.Waypoints[0];
        Assert.Equal("Abbey St Bathans", first.Name);
        Assert.Equal(55.0 + 51.267 / 60.0, first.Latitude, 5);
        Assert.Equal(-(2.0 + 23.525 / 60.0), first.Longitude, 5);
    }

    [Fact]
    public void TaskWithObsZonesRoundTrips()
    {
        var waypoints = new List<Waypoint>
        {
            new("Start", "STA", 52.0, -1.0),
            new("Turn 1", "TP1", 52.5, -0.5),
            new("Finish", "FIN", 52.0, -1.0),
        };
        var task = new CupTask(
            "Test Task",
            new[] { "Start", "Turn 1", "Finish" },
            new[]
            {
                ObservationZone.LineZone(0, 2.5),
                ObservationZone.Cylinder(1, 6.4),
                ObservationZone.Cylinder(2, 0.5),
            });

        var sw = new StringWriter();
        new CupWriter().Write(sw, waypoints, task);

        var reader = new CupReader();
        reader.Parse(new StringReader(sw.ToString()));

        Assert.Equal(3, reader.Waypoints.Count);
        Assert.Single(reader.Tasks);
        CupTask read = reader.Tasks[0];
        Assert.Equal("Test Task", read.Description);
        Assert.Equal(new[] { "Start", "Turn 1", "Finish" }, read.WaypointNames);
        Assert.Equal(3, read.Zones.Count);
        // TP1 cylinder radius preserved (6.4 km -> 6400 m).
        ObservationZone tp1 = read.Zones[1];
        Assert.Equal(6400.0, tp1.R1Metres, 1);
        Assert.Equal(ZoneStyle.Symmetrical, tp1.Style);
        // Start is a line zone.
        Assert.True(read.Zones[0].IsLine);
    }

    [Fact]
    public void WaypointUserDataRoundTrips()
    {
        var waypoints = new List<Waypoint>
        {
            new("Start", "STA", 52.0, -1.0, style: 4),
            new("Turn 1", "TP1", 52.5, -0.5, description: "TP", userData: "{\"rmin\":3,\"rmax\":9}"),
            new("Finish", "FIN", 52.0, -1.0, style: 4),
        };

        var sw = new StringWriter();
        new CupWriter().Write(sw, waypoints);

        var reader = new CupReader();
        reader.Parse(new StringReader(sw.ToString()));

        Assert.Equal("{\"rmin\":3,\"rmax\":9}", reader.Waypoints[1].UserData);
        // Waypoints without userdata keep an empty field.
        Assert.Equal(string.Empty, reader.Waypoints[0].UserData);
    }
}

public class FleetReaderTests
{
    [Fact]
    public void ReadsFleetWithHeader()
    {
        const string csv =
            "Type,Registration,CompNumber,Handicap\n" +
            "ASW 19,G-DDHT,NX,93.0\n" +
            "JS3-18m,G-JSML,ML,111.5\n";

        Fleet fleet = FleetReader.Read(new StringReader(csv), vRefCruKmh: 130.0);

        Assert.Equal(2, fleet.Gliders.Count);
        Assert.Equal("ASW 19", fleet.Gliders[0].Type);
        Assert.Equal("NX", fleet.Gliders[0].CompNumber);
        Assert.Equal(93.0, fleet.Gliders[0].Handicap, 6);
        Assert.Equal(111.5, fleet.MaxHandicap, 6);
        Assert.Equal(130.0, fleet.VRefCruKmh, 6);
    }

    [Fact]
    public void SkipsBlankAndCommentLines()
    {
        const string csv =
            "# my fleet\n" +
            "\n" +
            "ASW 19,G-DDHT,NX,93.0\n";

        Fleet fleet = FleetReader.Read(new StringReader(csv), 130.0);
        Assert.Single(fleet.Gliders);
    }
}
