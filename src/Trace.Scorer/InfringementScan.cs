using Trace;
using Trace.Airspace;

namespace Trace.Scorer;

/// <summary>
/// Scans an IGC trace for controlled-airspace infringements (airspace.md).
/// Reuses the checker's nearest-N cache, since consecutive fixes cluster.
/// </summary>
public static class InfringementScan
{
    private const double MetresToFeet = 3.280839895;

    /// <summary>One contiguous run of fixes inside a single airspace volume.</summary>
    public readonly record struct Infringement(
        string AirspaceClass, string AirspaceName, DateTime Enter, DateTime Exit, int FixCount);

    public static IReadOnlyList<Infringement> Scan(
        AirspaceChecker checker, IReadOnlyList<TracePoint> trace, IReadOnlySet<string>? classes = null)
    {
        var result = new List<Infringement>();

        AirspaceVolume? currentZone = null;
        DateTime enter = default;
        DateTime last = default;
        int count = 0;

        foreach (TracePoint fix in trace)
        {
            double altFt = fix.AltGps * MetresToFeet;
            AirspaceVolume? hit = checker.PointInAirspace(fix.Northings, fix.Eastings, altFt, classes);

            if (!ReferenceEquals(hit, currentZone))
            {
                if (currentZone != null)
                {
                    result.Add(new Infringement(currentZone.Class, currentZone.Name, enter, last, count));
                }

                currentZone = hit;
                enter = fix.When;
                count = 0;
            }

            if (hit != null)
            {
                count++;
                last = fix.When;
            }
        }

        if (currentZone != null)
        {
            result.Add(new Infringement(currentZone.Class, currentZone.Name, enter, last, count));
        }

        return result;
    }
}
