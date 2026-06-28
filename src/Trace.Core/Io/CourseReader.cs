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

        // Drop a leading takeoff / trailing landing if they duplicate the
        // adjacent start/finish waypoint (common CUP convention).
        List<string> names = task.WaypointNames.ToList();
        if (names.Count >= 2 && names[0].Equals(names[1], StringComparison.OrdinalIgnoreCase))
        {
            names.RemoveAt(0);
        }

        if (names.Count >= 2 && names[^1].Equals(names[^2], StringComparison.OrdinalIgnoreCase))
        {
            names.RemoveAt(names.Count - 1);
        }

        if (names.Count < 2)
        {
            throw new InvalidDataException("CUP task needs at least a start and finish.");
        }

        var points = new List<CoursePoint>();
        for (int i = 0; i < names.Count; i++)
        {
            if (!byName.TryGetValue(names[i], out Waypoint? wp))
            {
                throw new InvalidDataException($"Task waypoint '{names[i]}' not found in the file's waypoint table.");
            }

            CoursePointType type = i == 0 ? CoursePointType.Start
                : i == names.Count - 1 ? CoursePointType.Finish
                : CoursePointType.Turnpoint;

            double radius = type switch
            {
                CoursePointType.Start => DefaultStartRadiusKm,
                CoursePointType.Finish => DefaultFinishRadiusKm,
                _ => DefaultTurnpointRadiusKm,
            };

            points.Add(new CoursePoint(wp.Name, wp.Latitude, wp.Longitude, type, radius));
        }

        return new Course(points, task.Description);
    }
}
