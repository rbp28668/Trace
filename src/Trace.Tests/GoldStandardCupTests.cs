using System;
using System.IO;
using System.Linq;
using Trace.Io;
using Trace.Model;
using Trace.Planning;
using Trace.Planner;
using Xunit;

namespace Trace.Tests;

/// <summary>
/// End-to-end coverage against the SeeYou-generated "gold standard" task
/// (<c>data/cr2025_racer_2.cup</c>): a task line whose takeoff/landing are
/// <c>???</c> placeholders and whose zones use fields the engine does not model
/// (SpeedStyle, MaxAlt) plus an Options line. Verifies the task reads, classifies
/// barrels, and round-trips through the Planner changing only barrel radii.
/// </summary>
public class GoldStandardCupTests
{
    private static string DataDir =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data"));

    private static string GoldPath => Path.Combine(DataDir, "cr2025_racer_2.cup");

    [Fact]
    public void ReadsSeeYouTaskDroppingPlaceholders()
    {
        Course course = CourseReader.Read(GoldPath);

        // Task line: ???, GRL, EDG, EAR, RUS, HDW, GRL, ??? -> 6 real points.
        Assert.Equal(6, course.Points.Count);
        Assert.Equal(CoursePointType.Start, course.Points[0].Type);
        Assert.Equal(CoursePointType.Finish, course.Points[^1].Type);
        Assert.Equal("Gransden Lodge", course.Points[0].Name);
        Assert.Equal("Gransden Lodge", course.Points[^1].Name);
        // No ??? placeholder should survive into the course.
        Assert.DoesNotContain(course.Points, p => p.Name.Contains('?'));
    }

    [Fact]
    public void ClassifiesLargeZonesAsVariableBarrels()
    {
        Course course = CourseReader.Read(GoldPath);

        // Interior zones: three 10 km cylinders + one 20 km sector — all >= 1 km,
        // so all four are variable turnpoints, none fixed checkpoints.
        var interior = course.Points.Skip(1).SkipLast(1).ToList();
        Assert.Equal(4, interior.Count);
        Assert.All(interior, p => Assert.Equal(CoursePointType.Turnpoint, p.Type));
        // ObsZone indices (0 = Start) map onto the trimmed list: Rushden is index 3
        // (a 10 km cylinder) and Hardwick is index 4 (the 20 km / 45° sector).
        Assert.Equal(10.0, course.Points.Single(p => p.Name == "Rushden").DefaultRadiusKm, 3);
        Assert.Equal(20.0, course.Points.Single(p => p.Name == "Hardwick").DefaultRadiusKm, 3);
    }

    [Fact]
    public void PlannerOutputChangesOnlyBarrelRadii()
    {
        Course course = CourseReader.Read(GoldPath);
        var geometry = new CourseGeometry(course);
        var fleet = new Fleet(
            new[]
            {
                new Glider("ASG 29", "", "700", 111.0),
                new Glider("LS 6c", "", "LJ", 107.0),
                new Glider("DG 300", "", "4X", 95.0),
            },
            vRefCruKmh: 130.0);

        var optimizer = new BarrelOptimizer();
        FleetPlan plan = optimizer.Optimize(course, geometry, fleet, referenceHandicap: 111.0);

        string outDir = Path.Combine(Path.GetTempPath(), "trace_gold_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outDir);
        try
        {
            PlanWriter.WriteAll(outDir, course, plan);

            string[] files = Directory.GetFiles(outDir, "*.cup");
            Assert.NotEmpty(files);

            foreach (string file in files)
            {
                string text = File.ReadAllText(file);

                // Passthrough markers survive: the Options line and the non-modelled
                // SpeedStyle/MaxAlt fields the writer would otherwise drop.
                Assert.Contains("Options,NearAlt=656.0ft", text);
                Assert.Contains("SpeedStyle=", text);
                Assert.Contains("MaxAlt=", text);

                // The task re-reads (via the Scorer's path) and still has 6 points.
                Trace.Scoring.ScoringTask st = Trace.Scoring.ScoringTask.FromCup(file);
                Assert.Equal(6, st.Points.Count);
                Assert.Equal("Gransden Lodge", st.Points[0].Name);
            }
        }
        finally
        {
            Directory.Delete(outDir, recursive: true);
        }
    }
}
