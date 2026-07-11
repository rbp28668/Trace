using System.Globalization;
using System.Text.RegularExpressions;
using Trace.Geometry;
using Trace.Model;
using Trace.Rendering;
using Xunit;

namespace Trace.Tests;

public class TaskDiagramTests
{
    // A right-angle dogleg: Start (52.0,-1.0) -> TP1 (52.3,-1.0, due N) ->
    // Finish (52.3,-0.6, due E). At TP1 the inbound leg comes from the S and the
    // outbound goes E, so a symmetric sector opens OUTWARD to the NW.
    private static Course Dogleg()
    {
        var tpZone = new ObservationZone
        {
            PointIndex = 1,
            Style = ZoneStyle.Symmetrical,
            R1Metres = 10000.0,
            A1Degrees = 45.0,
            R2Metres = 5000.0,
        };

        return new Course(new[]
        {
            new CoursePoint("Start", 52.0, -1.0, CoursePointType.Start, 5.0,
                new ObservationZone { PointIndex = 0, Style = ZoneStyle.ToNext, R1Metres = 5000.0, A1Degrees = 90.0, IsLine = true }),
            new CoursePoint("TP1", 52.3, -1.0, CoursePointType.Turnpoint, 0.5, tpZone),
            new CoursePoint("Finish", 52.3, -0.6, CoursePointType.Finish, 3.0,
                new ObservationZone { PointIndex = 2, Style = ZoneStyle.ToPrevious, R1Metres = 3000.0, A1Degrees = 180.0 }),
        }, description: "Dogleg");
    }

    [Fact]
    public void RendersWellFormedSvgWithAllPoints()
    {
        string svg = TaskDiagram.Render(Dogleg(), "Dogleg");

        Assert.StartsWith("<svg", svg);
        Assert.EndsWith("</svg>\n", svg);
        Assert.Contains("Start", svg);
        Assert.Contains("TP1", svg);
        Assert.Contains("Finish", svg);
        // The turnpoint's barrel and sector are both labelled.
        Assert.Contains("barrel", svg);
        Assert.Contains("sector", svg);
    }

    [Fact]
    public void SymmetricSectorOpensOutward()
    {
        // The sector polygon's first vertex after the centre is at bearing
        // dir - A1. For an outward NW-facing sector (dir ~ 315°) the arc should
        // sweep through the NW/W quadrant: at least one sampled vertex must lie
        // to the NORTH-WEST of the turnpoint centre (screen: x < cx and y < cy).
        string svg = TaskDiagram.Render(Dogleg(), "Dogleg");

        // Grab the blue sector path (fill #3a7bd5) and its centre + vertices.
        Match m = Regex.Match(svg, "d=\"M([\\d.]+),([\\d.]+) ((?:L[\\d.]+,[\\d.]+ )+)Z\" fill=\"#3a7bd5\"");
        Assert.True(m.Success, "expected a blue sector path");

        double cx = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        double cy = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);

        var verts = Regex.Matches(m.Groups[3].Value, "L([\\d.]+),([\\d.]+)")
            .Select(v => (X: double.Parse(v.Groups[1].Value, CultureInfo.InvariantCulture),
                          Y: double.Parse(v.Groups[2].Value, CultureInfo.InvariantCulture)))
            .ToList();

        // Outward (NW) means some vertex is up-and-left of the centre; and NO
        // vertex points SE (down-and-right, which would be the inward direction).
        Assert.Contains(verts, v => v.X < cx && v.Y < cy);
        Assert.DoesNotContain(verts, v => v.X > cx + 1 && v.Y > cy + 1);
    }

    [Fact]
    public void SuppliedBarrelRadiiOverrideSourceZone()
    {
        // Draw a 7 km barrel at TP1 (index 1) even though its source R2 is 5 km.
        string svg = TaskDiagram.Render(Dogleg(), "Dogleg", new[] { 0.0, 7.0, 0.0 });
        Assert.Contains("barrel 7.0km", svg);
        Assert.DoesNotContain("barrel 5.0km", svg);
    }
}
