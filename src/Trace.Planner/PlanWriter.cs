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
            writer.Write(sw, BuildWaypoints(course), BuildTask(course, representative));
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
        var names = course.Points.Select(p => p.Name).ToList();
        var zones = new List<ObservationZone>();

        for (int i = 0; i < course.Points.Count; i++)
        {
            CoursePoint p = course.Points[i];
            zones.Add(p.Type switch
            {
                CoursePointType.Start => ObservationZone.LineZone(i, p.DefaultRadiusKm),
                _ => ObservationZone.Cylinder(i, plan.RadiiKm[i]),
            });
        }

        string desc = $"{course.Description} H{plan.Glider.Handicap.ToString("0.#", CultureInfo.InvariantCulture)}".Trim();
        return new CupTask(desc, names, zones);
    }

    private static string ShortCode(string name)
    {
        string upper = new string(name.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return upper.Length <= 6 ? upper : upper[..6];
    }
}
