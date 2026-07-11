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

        PrintBarrelTable(os, course, plan, args.VRefCru);
    }

    /// <summary>
    /// Prints the "Variable Barrel Sizes" table exactly as it appears on the task
    /// sheet: one row per distinct handicap (descending) with a single barrel
    /// <c>Radius</c> (every variable turnpoint shares it), the air-mass distance
    /// flown (<c>Act Dist</c> = V_a(H)·task time) and the classically handicapped
    /// distance (<c>Hcp Dist</c> = Act Dist·100/H). All values to one decimal place.
    /// </summary>
    private static void PrintBarrelTable(TextWriter os, Course course, FleetPlan plan, double vRefCru)
    {
        if (!VariableTurnpointIndices(course).Any())
        {
            return;
        }

        // One representative plan per distinct handicap, highest handicap first.
        var byHandicap = plan.Plans
            .GroupBy(p => p.Glider.Handicap)
            .OrderByDescending(g => g.Key)
            .Select(g => g.First())
            .ToList();

        os.WriteLine();
        os.WriteLine("Variable Barrel Sizes");
        os.WriteLine($"{"Handicap",8}  {"Radius",6}  {"Act Dist",8}  {"Hcp Dist",8}");

        foreach (GliderPlan gp in byHandicap)
        {
            double h = gp.Glider.Handicap;
            double radiusKm = VariableTurnpointIndices(course).Select(i => gp.RadiiKm[i]).First();
            double actDist = vRefCru * h / 100.0 * gp.DurationHours; // air-mass distance
            double hcpDist = h > 0 ? actDist * 100.0 / h : 0.0;

            os.WriteLine(
                $"{h,8:0.0}  {F1(radiusKm),6}  {F1(actDist),8}  {F1(hcpDist),8}");
        }
    }

    private static string F1(double v) => v.ToString("F1", CultureInfo.InvariantCulture);

    private static IEnumerable<int> VariableTurnpointIndices(Course course)
    {
        for (int i = 0; i < course.Points.Count; i++)
        {
            if (course.Points[i].Type == CoursePointType.Turnpoint)
            {
                yield return i;
            }
        }
    }

    private static IEnumerable<double> VariableRadii(Course course, GliderPlan gp)
        => VariableTurnpointIndices(course).Select(i => gp.RadiiKm[i]);

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
