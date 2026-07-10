using System.Globalization;
using Trace.Io;
using Trace.Model;
using Trace.Planning;

namespace Trace.Planner;

/// <summary>
/// Writes the optimiser output as one standard .CUP task file per handicap
/// (dht.md §5.1). Each file has fixed observation-zone radii for that handicap.
/// </summary>
public static class PlanWriter
{
    public static void WriteAll(string outDir, Course course, FleetPlan plan)
    {
        // One file per distinct handicap (gliders sharing a handicap share a task).
        var byHandicap = plan.Plans
            .GroupBy(p => p.Glider.Handicap)
            .OrderByDescending(g => g.Key);

        var writer = new CupWriter();
        foreach (var group in byHandicap)
        {
            GliderPlan representative = group.First();
            string file = Path.Combine(outDir, FileName(group.Key));
            using var sw = new StreamWriter(file);

            // When the course carries its source .cup, reproduce that file exactly
            // and change only the variable-barrel radii; otherwise synthesise a
            // standard task from the course model.
            IReadOnlyList<Waypoint> waypoints = course.SourceWaypoints ?? BuildWaypoints(course);
            writer.Write(sw, waypoints, BuildTask(course, representative));
        }
    }

    private static string FileName(double handicap)
        => $"task_h{handicap.ToString("0.#", CultureInfo.InvariantCulture).Replace('.', '_')}.cup";

    private static List<Waypoint> BuildWaypoints(Course course)
    {
        var waypoints = new List<Waypoint>();
        foreach (CoursePoint p in course.Points)
        {
            int style = p.Type == CoursePointType.Finish || p.Type == CoursePointType.Start ? 4 : 1;
            waypoints.Add(new Waypoint(p.Name, ShortCode(p.Name), p.Latitude, p.Longitude, style));
        }

        return waypoints;
    }

    private static CupTask BuildTask(Course course, GliderPlan plan)
    {
        string desc = $"{course.Description} H{plan.Glider.Handicap.ToString("0.#", CultureInfo.InvariantCulture)}".Trim();

        // Faithful path: keep the original task line, options and every zone,
        // substituting only R1 on the variable barrels (identified by point type).
        if (course.SourceTaskNames != null)
        {
            var zones = new List<ObservationZone>();
            for (int i = 0; i < course.Points.Count; i++)
            {
                CoursePoint p = course.Points[i];
                if (p.SourceZone == null)
                {
                    continue;
                }

                zones.Add(p.Type == CoursePointType.Turnpoint
                    ? p.SourceZone.WithR1(plan.RadiiKm[i] * 1000.0)
                    : p.SourceZone);
            }

            return new CupTask(desc, course.SourceTaskNames, zones, course.OptionsLine);
        }

        // Synthesised path: build a standard task from the course model.
        var names = course.Points.Select(p => p.Name).ToList();
        var built = new List<ObservationZone>();
        for (int i = 0; i < course.Points.Count; i++)
        {
            CoursePoint p = course.Points[i];
            built.Add(p.Type switch
            {
                CoursePointType.Start => ObservationZone.LineZone(i, p.DefaultRadiusKm),
                _ => ObservationZone.Cylinder(i, plan.RadiiKm[i]),
            });
        }

        return new CupTask(desc, names, built);
    }

    private static string ShortCode(string name)
    {
        string upper = new string(name.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return upper.Length <= 6 ? upper : upper[..6];
    }
}
