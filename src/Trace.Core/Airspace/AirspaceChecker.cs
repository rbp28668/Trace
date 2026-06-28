using Trace.Geometry;

namespace Trace.Airspace;

/// <summary>
/// Stateful airspace checker (airspace.md). Holds a set of airspaces (loaded from
/// one or more OpenAir files) and answers two questions:
///
///  • point-in-airspace — for checking IGC trace fixes (infringement), and
///  • zone-intersects-airspace — for validating DHT barrels.
///
/// Both can be filtered by airspace class. For the sequential point checks the
/// checker caches the most recently matched airspaces, since consecutive trace
/// fixes are geographically close and usually hit the same volume.
/// </summary>
public class AirspaceChecker
{
    private readonly List<AirspaceVolume> airspaces;
    private readonly LinkedList<AirspaceVolume> recent = new();
    private readonly int recentCapacity;

    public AirspaceChecker(IEnumerable<AirspaceVolume> airspaces, int recentCapacity = 8)
    {
        this.airspaces = airspaces.ToList();
        this.recentCapacity = Math.Max(1, recentCapacity);
    }

    public IReadOnlyList<AirspaceVolume> Airspaces => airspaces;

    /// <summary>
    /// Returns the airspace containing the given point at the given altitude, or
    /// null. <paramref name="classes"/> (case-insensitive) restricts which
    /// classes are considered; null/empty means all classes.
    /// </summary>
    public AirspaceVolume? PointInAirspace(double lat, double lon, double altitudeFt,
        IReadOnlySet<string>? classes = null)
    {
        // Check recently-matched airspaces first (adjacent fixes cluster).
        foreach (AirspaceVolume a in recent)
        {
            if (Matches(a, lat, lon, altitudeFt, classes))
            {
                Promote(a);
                return a;
            }
        }

        foreach (AirspaceVolume a in airspaces)
        {
            if (Matches(a, lat, lon, altitudeFt, classes))
            {
                Remember(a);
                return a;
            }
        }

        return null;
    }

    /// <summary>True if the point lies in any (optionally class-filtered) airspace.</summary>
    public bool IsInfringing(double lat, double lon, double altitudeFt,
        IReadOnlySet<string>? classes = null)
        => PointInAirspace(lat, lon, altitudeFt, classes) != null;

    /// <summary>
    /// Returns every airspace that an observation zone (cylinder of the given
    /// radius, reaching up to <paramref name="zoneCeilingFt"/>) intersects.
    /// AirspaceVolume whose base is above the zone ceiling is ignored (airspace.md).
    /// </summary>
    public IReadOnlyList<AirspaceVolume> ZoneIntersections(double lat, double lon, double radiusKm,
        double zoneCeilingFt, IReadOnlySet<string>? classes = null)
    {
        var hits = new List<AirspaceVolume>();
        foreach (AirspaceVolume a in airspaces)
        {
            if (classes is { Count: > 0 } && !classes.Contains(a.Class))
            {
                continue;
            }

            // Vertical overlap: ignore airspace entirely above the zone ceiling.
            if (a.LowerLimitFt > zoneCeilingFt)
            {
                continue;
            }

            // Cheap bounding-box reject with a radius margin (~deg) before the
            // exact polygon/circle test.
            double margin = radiusKm / 100.0; // ~1 deg ≈ 111 km
            if (lat < a.MinLatitude - margin || lat > a.MaxLatitude + margin ||
                lon < a.MinLongitude - margin || lon > a.MaxLongitude + margin)
            {
                continue;
            }

            if (Polygon.IntersectsCircle(a.Boundary, lat, lon, radiusKm))
            {
                hits.Add(a);
            }
        }

        return hits;
    }

    private static bool Matches(AirspaceVolume a, double lat, double lon, double altitudeFt,
        IReadOnlySet<string>? classes)
    {
        if (classes is { Count: > 0 } && !classes.Contains(a.Class))
        {
            return false;
        }

        if (altitudeFt < a.LowerLimitFt || altitudeFt > a.UpperLimitFt)
        {
            return false;
        }

        if (lat < a.MinLatitude || lat > a.MaxLatitude ||
            lon < a.MinLongitude || lon > a.MaxLongitude)
        {
            return false;
        }

        return Polygon.Contains(a.Boundary, lat, lon);
    }

    private void Remember(AirspaceVolume a)
    {
        recent.AddFirst(a);
        if (recent.Count > recentCapacity)
        {
            recent.RemoveLast();
        }
    }

    private void Promote(AirspaceVolume a)
    {
        if (!ReferenceEquals(recent.First?.Value, a))
        {
            recent.Remove(a);
            recent.AddFirst(a);
        }
    }
}
