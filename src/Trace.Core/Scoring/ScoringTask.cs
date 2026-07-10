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

        // Trim the CUP takeoff/landing (including ??? placeholders) so what remains
        // is Start → turnpoints → Finish; the trimmed index then equals each
        // point's ObsZone number (0 = Start, per spec).
        List<string> names = task.WaypointNames.ToList();
        CupTaskLayout.Trim(names, task.Zones.Count);

        var zonesByIndex = task.Zones.ToDictionary(z => z.PointIndex);

        var points = new List<ScoringPoint>();
        for (int i = 0; i < names.Count; i++)
        {
            if (!byName.TryGetValue(names[i], out Waypoint? wp))
            {
                throw new InvalidDataException($"Task waypoint '{names[i]}' not found in waypoint table.");
            }

            // Interior points keep whatever radius their zone declares; a control
            // point (fixed, small radius) scores identically to a turnpoint here.
            bool isStart = i == 0;
            bool isFinish = i == names.Count - 1;
            zonesByIndex.TryGetValue(i, out ObservationZone? z);
            double radiusKm = z?.R1Metres / 1000.0 ?? 0.5;

            CoursePointType type = isStart ? CoursePointType.Start
                : isFinish ? CoursePointType.Finish
                : z != null && radiusKm < CourseReader.VariableBarrelThresholdKm
                    ? CoursePointType.Checkpoint
                    : CoursePointType.Turnpoint;

            points.Add(new ScoringPoint(wp.Name, wp.Latitude, wp.Longitude, type, radiusKm));
        }

        return new ScoringTask(points, task.Description);
    }
}
