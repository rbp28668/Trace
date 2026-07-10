namespace Trace.Model;

/// <summary>
/// A task read from (or written to) the <c>-----Related Tasks-----</c> section
/// of a SeeYou .CUP file: an ordered list of waypoint names with their
/// observation zones. By CUP convention the first and last entries are the
/// takeoff and landing.
/// </summary>
public class CupTask
{
    public CupTask(string description, IReadOnlyList<string> waypointNames,
        IReadOnlyList<ObservationZone> zones, string? optionsLine = null)
    {
        Description = description;
        WaypointNames = waypointNames;
        Zones = zones;
        OptionsLine = optionsLine;
    }

    public string Description { get; }

    /// <summary>Ordered waypoint names (takeoff, start, turnpoints…, finish, landing).</summary>
    public IReadOnlyList<string> WaypointNames { get; }

    /// <summary>Observation zones keyed by their <see cref="ObservationZone.PointIndex"/>.</summary>
    public IReadOnlyList<ObservationZone> Zones { get; }

    /// <summary>
    /// The verbatim <c>Options,…</c> line for this task, if the source file had one
    /// (e.g. <c>Options,NearAlt=656.0ft</c>). Preserved for faithful re-emit; the
    /// DHT engine does not otherwise interpret it. Null when absent.
    /// </summary>
    public string? OptionsLine { get; }
}
