using Trace.Airspace;
using Trace.Model;
using Trace.Planning;

namespace Trace.Planner;

/// <summary>
/// Cross-references optimised barrels against controlled airspace (dht.md §5.2):
/// if any glider's barrel penetrates controlled airspace, task publication must
/// be blocked.
/// </summary>
public static class AirspaceGuard
{
    /// <summary>A barrel that intersects controlled airspace.</summary>
    public readonly record struct Violation(
        string CompNumber, string PointName, double RadiusKm, string AirspaceClass, string AirspaceName);

    public static IReadOnlyList<Violation> Check(
        Course course, FleetPlan plan, AirspaceChecker checker, double maxHeightFt)
    {
        var violations = new List<Violation>();
        foreach (GliderPlan gp in plan.Plans)
        {
            for (int i = 0; i < course.Points.Count; i++)
            {
                CoursePoint p = course.Points[i];
                if (p.Type != CoursePointType.Turnpoint)
                {
                    continue;
                }

                double radiusKm = gp.RadiiKm[i];
                IReadOnlyList<AirspaceVolume> hits = checker.ZoneIntersections(
                    p.Latitude, p.Longitude, radiusKm, maxHeightFt, AirspaceLoader.ControlledClasses);

                // Distinct by class+name: a single named airspace is often
                // modelled as several stacked sub-volumes in OpenAir.
                foreach (string name in hits.Select(a => $"{a.Class}|{a.Name}").Distinct())
                {
                    string[] parts = name.Split('|', 2);
                    violations.Add(new Violation(gp.Glider.CompNumber, p.Name, radiusKm, parts[0], parts[1]));
                }
            }
        }

        return violations;
    }
}
