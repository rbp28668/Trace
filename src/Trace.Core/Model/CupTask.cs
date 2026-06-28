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
        IReadOnlyList<ObservationZone> zones)
    {
        Description = description;
        WaypointNames = waypointNames;
        Zones = zones;
    }

    public string Description { get; }

    /// <summary>Ordered waypoint names (takeoff, start, turnpoints…, finish, landing).</summary>
    public IReadOnlyList<string> WaypointNames { get; }

    /// <summary>Observation zones keyed by their <see cref="ObservationZone.PointIndex"/>.</summary>
    public IReadOnlyList<ObservationZone> Zones { get; }
}
