using Trace.Io;
using Trace.Model;

namespace Trace.Scoring;

/// <summary>One point of a task being scored: its centre and observation radius.</summary>
public readonly record struct ScoringPoint(
    string Name, double Latitude, double Longitude, CoursePointType Type, double RadiusKm);

/// <summary>
/// A task as flown for scoring (dht.md §4.2): the ordered points with the actual
/// per-handicap barrel radii read from a Planner-generated .CUP file. Unlike
/// <see cref="Course"/> (which carries default radii for planning), the radii
/// here come from the file's ObsZone lines.
/// </summary>
public class ScoringTask
{
    public ScoringTask(IReadOnlyList<ScoringPoint> points, string description = "")
    {
        Points = points;
        Description = description;
    }

    public string Description { get; }

    public IReadOnlyList<ScoringPoint> Points { get; }

    /// <summary>Builds a scoring task from a .CUP file containing a task block.</summary>
    public static ScoringTask FromCup(string path)
    {
        var reader = new CupReader();
        reader.Parse(path);
        return FromCup(reader);
    }

    public static ScoringTask FromCup(TextReader textReader)
    {
        var reader = new CupReader();
        reader.Parse(textReader);
        return FromCup(reader);
    }

    private static ScoringTask FromCup(CupReader reader)
    {
        if (reader.Tasks.Count == 0)
        {
            throw new InvalidDataException("CUP task file contains no task block.");
        }

        CupTask task = reader.Tasks[0];

        var byName = new Dictionary<string, Waypoint>(StringComparer.OrdinalIgnoreCase);
        foreach (Waypoint w in reader.Waypoints)
        {
            byName.TryAdd(w.Name, w);
        }

        // Drop a duplicated leading takeoff / trailing landing (CUP convention),
        // tracking how many leading points were trimmed so ObsZone indices (which
        // refer to the original list) still line up.
        List<string> names = task.WaypointNames.ToList();
        int leadingTrim = 0;
        if (names.Count >= 2 && names[0].Equals(names[1], StringComparison.OrdinalIgnoreCase))
        {
            names.RemoveAt(0);
            leadingTrim = 1;
        }

        if (names.Count >= 2 && names[^1].Equals(names[^2], StringComparison.OrdinalIgnoreCase))
        {
            names.RemoveAt(names.Count - 1);
        }

        var zonesByIndex = task.Zones.ToDictionary(z => z.PointIndex);

        var points = new List<ScoringPoint>();
        for (int i = 0; i < names.Count; i++)
        {
            if (!byName.TryGetValue(names[i], out Waypoint? wp))
            {
                throw new InvalidDataException($"Task waypoint '{names[i]}' not found in waypoint table.");
            }

            CoursePointType type = i == 0 ? CoursePointType.Start
                : i == names.Count - 1 ? CoursePointType.Finish
                : CoursePointType.Turnpoint;

            double radiusKm = zonesByIndex.TryGetValue(i + leadingTrim, out ObservationZone? z)
                ? z.R1Metres / 1000.0
                : 0.5;

            points.Add(new ScoringPoint(wp.Name, wp.Latitude, wp.Longitude, type, radiusKm));
        }

        return new ScoringTask(points, task.Description);
    }
}
