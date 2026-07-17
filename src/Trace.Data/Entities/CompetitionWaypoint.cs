namespace Trace.Data.Entities;

/// <summary>
/// A waypoint from the competition's uploaded .cup file, scoped to one
/// competition. Task turnpoints are constrained to this list, taking their
/// coordinates from the chosen waypoint. Maps from <see cref="Trace.Model.Waypoint"/>.
/// </summary>
public class CompetitionWaypoint
{
    public int Id { get; set; }

    public int CompetitionId { get; set; }

    public Competition? Competition { get; set; }

    /// <summary>Full waypoint name (CUP <c>name</c> column); the task turnpoint key.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Short code (CUP <c>code</c> column).</summary>
    public string? Code { get; set; }

    /// <summary>Decimal degrees, north +ve.</summary>
    public double Latitude { get; set; }

    /// <summary>Decimal degrees, east +ve.</summary>
    public double Longitude { get; set; }

    /// <summary>CUP waypoint style code (1 = waypoint, 4 = gliding airfield, …).</summary>
    public int Style { get; set; }

    /// <summary>Elevation in metres.</summary>
    public double ElevationM { get; set; }
}
