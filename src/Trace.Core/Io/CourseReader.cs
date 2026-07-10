using System.Text.Json;
using Trace.Model;

namespace Trace.Io;

/// <summary>
/// Builds a baseline <see cref="Course"/> from a SeeYou .CUP file that contains a
/// task block (implementation-plan §4: ".cup course"). Waypoint names in the task
/// are resolved against the file's own waypoint table. Roles are assigned by
/// position: first point = Start, last = Finish, interior = Turnpoint.
///
/// By CUP convention a task line begins and ends with the takeoff and landing.
/// These are dropped unless they coincide with the start/finish; the resulting
/// course runs Start → turnpoints → Finish.
/// </summary>
public static class CourseReader
{
    /// <summary>Default start line half-width (km) when the task gives no zone.</summary>
    public const double DefaultStartRadiusKm = 5.0;

    /// <summary>Default finish cylinder radius (km).</summary>
    public const double DefaultFinishRadiusKm = 3.0;

    /// <summary>Default/min turnpoint radius (km).</summary>
    public const double DefaultTurnpointRadiusKm = 0.5;

    /// <summary>
    /// Interior zones at or above this radius (km) are variable barrels the
    /// optimiser may resize; smaller ones are fixed control points (checkpoints).
    /// </summary>
    public const double VariableBarrelThresholdKm = 1.0;

    public static Course Read(string path)
    {
        var reader = new CupReader();
        reader.Parse(path);
        return Build(reader);
    }

    public static Course Read(TextReader textReader)
    {
        var reader = new CupReader();
        reader.Parse(textReader);
        return Build(reader);
    }

    private static Course Build(CupReader reader)
    {
        if (reader.Tasks.Count == 0)
        {
            throw new InvalidDataException("CUP course file contains no task block.");
        }

        CupTask task = reader.Tasks[0];

        // First occurrence wins: a waypoint may legitimately appear twice in the
        // table (e.g. the same airfield used as both start and finish).
        var byName = new Dictionary<string, Waypoint>(StringComparer.OrdinalIgnoreCase);
        foreach (Waypoint w in reader.Waypoints)
        {
            byName.TryAdd(w.Name, w);
        }

        // Trim the CUP takeoff/landing so what remains is Start → turnpoints →
        // Finish; the trimmed index then equals each point's ObsZone number.
        List<string> names = task.WaypointNames.ToList();
        CupTaskLayout.Trim(names, task.Zones.Count);

        if (names.Count < 2)
        {
            throw new InvalidDataException("CUP task needs at least a start and finish.");
        }

        var zonesByIndex = task.Zones.ToDictionary(z => z.PointIndex);

        var points = new List<CoursePoint>();
        for (int i = 0; i < names.Count; i++)
        {
            if (!byName.TryGetValue(names[i], out Waypoint? wp))
            {
                throw new InvalidDataException($"Task waypoint '{names[i]}' not found in the file's waypoint table.");
            }

            bool isStart = i == 0;
            bool isFinish = i == names.Count - 1;
            zonesByIndex.TryGetValue(i, out ObservationZone? zone);

            // Radius comes from the file's ObsZone barrel when present, else a role
            // default. The DHT engine sizes the barrel (R2, or R1 for a cylinder).
            double radius = zone?.BarrelRadiusMetres / 1000.0 ?? (isStart ? DefaultStartRadiusKm
                : isFinish ? DefaultFinishRadiusKm
                : DefaultTurnpointRadiusKm);

            // An interior point whose file zone is below the barrel threshold is a
            // fixed control point; at/above it (or with no zone to say otherwise)
            // it is a variable turnpoint the optimiser may resize.
            (double? rMin, double? rMax) = ParseBounds(wp.UserData);

            // A point is a fixed checkpoint if its file zone is below the barrel
            // threshold; but an explicit rMin/rMax in userdata always marks it
            // variable (the task-setter said so, even at a small radius).
            bool hasExplicitBounds = rMin != null || rMax != null;
            CoursePointType type = isStart ? CoursePointType.Start
                : isFinish ? CoursePointType.Finish
                : !hasExplicitBounds && zone != null && radius < VariableBarrelThresholdKm
                    ? CoursePointType.Checkpoint
                    : CoursePointType.Turnpoint;

            points.Add(new CoursePoint(wp.Name, wp.Latitude, wp.Longitude, type, radius, zone, rMin, rMax));
        }

        return new Course(points, task.Description, task.WaypointNames, task.OptionsLine,
            reader.Waypoints.ToList());
    }

    /// <summary>
    /// Parses per-turnpoint barrel bounds from a waypoint's userdata JSON, e.g.
    /// <c>{"rmin":0.5,"rmax":10}</c> (km). Returns nulls for absent or unparseable
    /// data so the optimiser falls back to its global bounds.
    /// </summary>
    private static (double? RMinKm, double? RMaxKm) ParseBounds(string userData)
    {
        if (string.IsNullOrWhiteSpace(userData) || !userData.TrimStart().StartsWith('{'))
        {
            return (null, null);
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(userData);
            JsonElement root = doc.RootElement;
            double? min = root.TryGetProperty("rmin", out JsonElement e1) && e1.TryGetDouble(out double m) ? m : null;
            double? max = root.TryGetProperty("rmax", out JsonElement e2) && e2.TryGetDouble(out double x) ? x : null;
            return (min, max);
        }
        catch (JsonException)
        {
            return (null, null); // ignore malformed userdata rather than failing the read
        }
    }
}
