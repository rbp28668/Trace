using Trace.Geometry;
using Trace.Model;
using Trace.Planning;
using Xunit;

namespace Trace.Tests;

public class BarrelOptimizerTests
{
    // A two-turnpoint course with two 90° corners (Start N to TP1, E to TP2,
    // S to Finish), ~123 km total. Two corners give enough corner-cutting
    // capacity to handicap the 93–114 spread within R_max.
    private static Course MakeDogleg()
    {
        var points = new List<CoursePoint>
        {
            new("Start", 52.0, -1.0, CoursePointType.Start, 5.0),
            new("TP1", 52.4, -1.0, CoursePointType.Turnpoint, 0.5),
            new("TP2", 52.4, -0.5, CoursePointType.Turnpoint, 0.5),
            new("Finish", 52.0, -0.5, CoursePointType.Finish, 3.0),
        };
        return new Course(points);
    }

    private static Fleet MakeFleet() => new(
        new[]
        {
            new Glider("JS3", "G-JS", "J1", 114.0),  // reference (highest)
            new Glider("LS8", "G-LS", "L8", 100.0),
            new Glider("ASW19", "G-AS", "A1", 93.0),
        },
        vRefCruKmh: 130.0);

    [Fact]
    public void AllGlidersConvergeToReferenceTimeInNilWind()
    {
        Course course = MakeDogleg();
        var geometry = new CourseGeometry(course);
        Fleet fleet = MakeFleet();

        var opt = new BarrelOptimizer(new OptimizerOptions { ToleranceSeconds = 5.0 });
        FleetPlan plan = opt.Optimize(course, geometry, fleet, referenceHandicap: 114.0);

        double tRefSeconds = plan.ReferenceDurationHours * 3600.0;
        foreach (GliderPlan gp in plan.Plans)
        {
            Assert.True(gp.Converged, $"{gp.Glider.Type} did not converge");
            double tSeconds = gp.DurationHours * 3600.0;
            Assert.True(Math.Abs(tSeconds - tRefSeconds) <= 5.0,
                $"{gp.Glider.Type}: {tSeconds:F1}s vs ref {tRefSeconds:F1}s");
        }
    }

    [Fact]
    public void ReferenceGliderUsesMinimumBarrel()
    {
        Course course = MakeDogleg();
        var geometry = new CourseGeometry(course);
        Fleet fleet = MakeFleet();

        var opt = new BarrelOptimizer();
        FleetPlan plan = opt.Optimize(course, geometry, fleet, referenceHandicap: 114.0);

        GliderPlan js3 = plan.Plans.Single(p => p.Glider.Handicap == 114.0);
        Assert.Equal(0.5, js3.RadiiKm[1], 3); // TP at R_min
    }

    [Fact]
    public void LowerHandicapGetsLargerBarrelAndShorterDistance()
    {
        Course course = MakeDogleg();
        var geometry = new CourseGeometry(course);
        Fleet fleet = MakeFleet();

        var opt = new BarrelOptimizer();
        FleetPlan plan = opt.Optimize(course, geometry, fleet, referenceHandicap: 114.0);

        GliderPlan js3 = plan.Plans.Single(p => p.Glider.Handicap == 114.0);
        GliderPlan asw = plan.Plans.Single(p => p.Glider.Handicap == 93.0);

        Assert.True(asw.RadiiKm[1] > js3.RadiiKm[1], "lower-H barrel should be larger");
        Assert.True(asw.EffectiveDistanceKm < js3.EffectiveDistanceKm,
            "lower-H glider should fly a shorter effective distance");
    }

    [Fact]
    public void DistanceRatioApproximatesHandicapRatioInNilWind()
    {
        Course course = MakeDogleg();
        var geometry = new CourseGeometry(course);
        Fleet fleet = MakeFleet();

        var opt = new BarrelOptimizer();
        FleetPlan plan = opt.Optimize(course, geometry, fleet, referenceHandicap: 114.0);

        GliderPlan js3 = plan.Plans.Single(p => p.Glider.Handicap == 114.0);
        GliderPlan asw = plan.Plans.Single(p => p.Glider.Handicap == 93.0);

        // D_Target(H) = D_Ref * H/H_Ref (dht.md §3.4). Nil wind so time parity
        // reduces to distance parity scaled by airspeed (∝ handicap).
        double expectedRatio = 93.0 / 114.0;
        double actualRatio = asw.EffectiveDistanceKm / js3.EffectiveDistanceKm;
        Assert.Equal(expectedRatio, actualRatio, 2);
    }

    [Fact]
    public void ConvergesUnderWind()
    {
        Course course = MakeDogleg();
        var geometry = new CourseGeometry(course);
        Fleet fleet = MakeFleet();

        var opt = new BarrelOptimizer { WindSpeed = 25.0, WindDirection = 270.0 };
        FleetPlan plan = opt.Optimize(course, geometry, fleet, referenceHandicap: 114.0);

        double tRefSeconds = plan.ReferenceDurationHours * 3600.0;
        foreach (GliderPlan gp in plan.Plans)
        {
            double tSeconds = gp.DurationHours * 3600.0;
            Assert.True(Math.Abs(tSeconds - tRefSeconds) <= 5.0,
                $"{gp.Glider.Type}: {tSeconds:F1}s vs ref {tRefSeconds:F1}s under wind");
        }
    }

    [Fact]
    public void PerTurnpointBoundsAreHonoured()
    {
        // TP1 is pinned to a raised floor of 3 km (its own RMin); TP2 keeps the
        // global 0.5 km floor. A low-handicap glider should never take TP1 below
        // 3 km, and the reference glider sits exactly on each point's floor.
        var points = new List<CoursePoint>
        {
            new("Start", 52.0, -1.0, CoursePointType.Start, 5.0),
            new("TP1", 52.4, -1.0, CoursePointType.Turnpoint, 3.0, rMinKm: 3.0, rMaxKm: 10.0),
            new("TP2", 52.4, -0.5, CoursePointType.Turnpoint, 0.5),
            new("Finish", 52.0, -0.5, CoursePointType.Finish, 3.0),
        };
        var course = new Course(points);
        var geometry = new CourseGeometry(course);
        Fleet fleet = MakeFleet();

        var opt = new BarrelOptimizer();
        FleetPlan plan = opt.Optimize(course, geometry, fleet, referenceHandicap: 114.0);

        foreach (GliderPlan gp in plan.Plans)
        {
            Assert.True(gp.RadiiKm[1] >= 3.0 - 1e-6, $"TP1 {gp.RadiiKm[1]} below its 3 km floor");
            Assert.True(gp.RadiiKm[2] >= 0.5 - 1e-6, $"TP2 {gp.RadiiKm[2]} below the global floor");
        }

        // Reference glider sits on each point's own minimum.
        GliderPlan js3 = plan.Plans.Single(p => p.Glider.Handicap == 114.0);
        Assert.Equal(3.0, js3.RadiiKm[1], 3);
        Assert.Equal(0.5, js3.RadiiKm[2], 3);
    }

    [Fact]
    public void RadiiStayWithinBounds()
    {
        Course course = MakeDogleg();
        var geometry = new CourseGeometry(course);
        Fleet fleet = MakeFleet();

        var opt = new BarrelOptimizer(new OptimizerOptions { RMinKm = 0.5, RMaxKm = 12.0 });
        FleetPlan plan = opt.Optimize(course, geometry, fleet, referenceHandicap: 114.0);

        foreach (GliderPlan gp in plan.Plans)
        {
            Assert.InRange(gp.RadiiKm[1], 0.5, 12.0);
        }
    }
}
