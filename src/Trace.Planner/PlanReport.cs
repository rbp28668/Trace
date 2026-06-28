using System.Globalization;
using Trace.Model;
using Trace.Planning;

namespace Trace.Planner;

/// <summary>Prints a human-readable summary of the optimiser run.</summary>
public static class PlanReport
{
    public static void Print(TextWriter os, Course course, FleetPlan plan, PlannerArgs args)
    {
        os.WriteLine($"Task: {course.Description}");
        os.WriteLine($"Points: {course.Points.Count}  " +
            $"Wind: {args.WindDirection:F0}°/{args.WindSpeed:F0} km/h  " +
            $"VRefCru(H100): {args.VRefCru:F0} km/h");
        os.WriteLine($"Reference handicap: {plan.ReferenceHandicap:0.#}");
        os.WriteLine($"Reference distance D_Ref: {plan.ReferenceDistanceKm:F1} km");
        os.WriteLine($"Reference time T_Ref: {FormatHms(plan.ReferenceDurationHours)}");
        os.WriteLine();

        // Header.
        os.WriteLine($"{"Comp",-5} {"Type",-10} {"H",5}  {"Dist(km)",9}  {"Time",9}  {"Δt(s)",7}  Radii(km)");

        double tRefSeconds = plan.ReferenceDurationHours * 3600.0;
        foreach (GliderPlan gp in plan.Plans)
        {
            double dt = gp.DurationHours * 3600.0 - tRefSeconds;
            string radii = string.Join(",", VariableRadii(course, gp)
                .Select(r => r.ToString("F1", CultureInfo.InvariantCulture)));

            os.WriteLine(
                $"{gp.Glider.CompNumber,-5} {Truncate(gp.Glider.Type, 10),-10} " +
                $"{gp.Glider.Handicap,5:0.#}  {gp.EffectiveDistanceKm,9:F1}  " +
                $"{FormatHms(gp.DurationHours),9}  {dt,7:+0.0;-0.0}  {radii}" +
                (gp.Converged ? "" : "  [NOT CONVERGED]"));

            foreach (string w in gp.Warnings)
            {
                os.WriteLine($"      ! {w}");
            }
        }
    }

    private static IEnumerable<double> VariableRadii(Course course, GliderPlan gp)
    {
        for (int i = 0; i < course.Points.Count; i++)
        {
            if (course.Points[i].Type == CoursePointType.Turnpoint)
            {
                yield return gp.RadiiKm[i];
            }
        }
    }

    private static string FormatHms(double hours)
    {
        if (double.IsInfinity(hours))
        {
            return "∞";
        }

        var ts = TimeSpan.FromHours(hours);
        return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
