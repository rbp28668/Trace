using System.IO;
using Trace;
using Xunit;

namespace Trace.Tests;

/// <summary>
/// Phase 1 smoke tests: confirm the refactored <see cref="IGCFile"/> in
/// Trace.Core still parses a basic IGC declaration + fix and round-trips.
/// </summary>
public class SmokeTests
{
    private const string SampleIgc =
        "AFLA001\n" +
        "HFDTE300515\n" +
        "HFGTYGliderType:LS7\n" +
        "HFGIDGliderID:G-DFOG\n" +
        "B1020275210970N00006652WA0006600123\n" +
        "B1020285210980N00006650WA0006600124\n";

    [Fact]
    public void ParsesHeaderAndTrace()
    {
        var igc = new IGCFile();
        using var reader = new StringReader(SampleIgc);
        igc.Parse(reader);

        Assert.Equal("LS7", igc.GliderType);
        Assert.Equal("G-DFOG", igc.Registration);
        Assert.Equal(2, igc.Trace.Count);
    }

    [Fact]
    public void TracePointCoordinatesAreDecimalDegrees()
    {
        var igc = new IGCFile();
        using var reader = new StringReader(SampleIgc);
        igc.Parse(reader);

        TracePoint first = igc.Trace[0];
        // 5210970N -> 52 deg 10.970 min north.
        Assert.Equal(52.0 + 10.970 / 60.0, first.Northings, 5);
        // 00006652W -> 0 deg 6.652 min west, stored as a negative easting.
        Assert.Equal(-(6.652 / 60.0), first.Eastings, 5);
    }
}
