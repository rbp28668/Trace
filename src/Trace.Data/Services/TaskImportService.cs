using Trace.Data.Entities;
using Trace.Io;
using Trace.Model;

namespace Trace.Data.Services;

/// <summary>A task parsed from a .cup file, ready to preview/commit.</summary>
public class ParsedTask
{
    public string Description { get; set; } = string.Empty;
    public string? OptionsLine { get; set; }
    public List<Turnpoint> Turnpoints { get; set; } = new();
}

/// <summary>
/// Parses a SeeYou .cup task (reusing <see cref="CupReader"/>) into
/// <see cref="Turnpoint"/> entities. Task-line names are resolved to coordinates
/// via the file's waypoint table, and <c>ObsZone</c> lines are paired to points by
/// their 0-based index. The leading/trailing <c>???</c> takeoff/landing entries in
/// the task line are skipped.
/// </summary>
public class TaskImportService
{
    /// <summary>Parses all tasks found in the .cup text.</summary>
    public static List<ParsedTask> Parse(string cupText)
    {
        var reader = new CupReader();
        reader.Parse(new StringReader(cupText));

        var byName = new Dictionary<string, Waypoint>(StringComparer.OrdinalIgnoreCase);
        foreach (Waypoint wp in reader.Waypoints)
        {
            byName[wp.Name] = wp;
        }

        var results = new List<ParsedTask>();
        foreach (CupTask task in reader.Tasks)
        {
            results.Add(BuildTask(task, byName));
        }

        return results;
    }

    /// <summary>
    /// Interior zones below this radius (km) are treated as fixed control points
    /// (checkpoints) rather than turnpoints. Mirrors
    /// <c>CourseReader.VariableBarrelThresholdKm</c>.
    /// </summary>
    private const double CheckpointThresholdKm = 1.0;

    private static ParsedTask BuildTask(CupTask task, IReadOnlyDictionary<string, Waypoint> byName)
    {
        var zonesByIndex = task.Zones.ToDictionary(z => z.PointIndex);

        // Trim the CUP takeoff/landing so the remaining index equals each point's
        // ObsZone number (same rule CourseReader uses).
        List<string> names = task.WaypointNames.ToList();
        CupTaskLayout.Trim(names, task.Zones.Count);

        var turnpoints = new List<Turnpoint>();
        for (int i = 0; i < names.Count; i++)
        {
            string name = names[i];
            byName.TryGetValue(name, out Waypoint? wp);
            zonesByIndex.TryGetValue(i, out ObservationZone? zone);

            bool isStart = i == 0;
            bool isFinish = i == names.Count - 1;

            // A small interior zone is a fixed checkpoint; start/finish are their
            // own roles; everything else is a turnpoint.
            double barrelKm = MetresToKm(zone?.BarrelRadiusMetres ?? 0.0);
            bool isCheckpoint = !isStart && !isFinish && zone != null &&
                                barrelKm < CheckpointThresholdKm;

            turnpoints.Add(new Turnpoint
            {
                Index = i,
                Waypoint = name,
                Latitude = wp?.Latitude ?? 0.0,
                Longitude = wp?.Longitude ?? 0.0,
                IsCheckpoint = isCheckpoint,
                IsLine = zone?.IsLine ?? false,
                Style = (int)(zone?.Style ?? ZoneStyle.Symmetrical),
                DirectionType = (int)(zone?.Style ?? ZoneStyle.Symmetrical),
                Radius1 = MetresToKm(zone?.R1Metres ?? 0.0),
                Angle1 = zone?.A1Degrees ?? 180.0,
                Radius2 = MetresToKm(zone?.R2Metres ?? 0.0),
                Angle2 = zone?.A2Degrees ?? 0.0,
            });
        }

        return new ParsedTask
        {
            Description = task.Description,
            OptionsLine = task.OptionsLine,
            Turnpoints = turnpoints,
        };
    }

    private static double MetresToKm(double metres) => metres / 1000.0;
}
