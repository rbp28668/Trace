namespace Trace.Model;

/// <summary>Role of a point in the baseline task course (dht.md §2.2).</summary>
public enum CoursePointType
{
    Start,
    Turnpoint,
    Checkpoint,
    Finish,
}

/// <summary>
/// A single point in the baseline race course: a waypoint with a role and a
/// default observation-zone radius (km). Turnpoints are candidates for variable
/// barrels; Start/Finish/Checkpoint keep fixed zones.
/// </summary>
public class CoursePoint
{
    public CoursePoint(string name, double latitude, double longitude,
        CoursePointType type, double defaultRadiusKm, ObservationZone? sourceZone = null,
        double? rMinKm = null, double? rMaxKm = null)
    {
        Name = name;
        Latitude = latitude;
        Longitude = longitude;
        Type = type;
        DefaultRadiusKm = defaultRadiusKm;
        SourceZone = sourceZone;
        RMinKm = rMinKm;
        RMaxKm = rMaxKm;
    }

    public string Name { get; }

    /// <summary>Decimal degrees, north +ve.</summary>
    public double Latitude { get; }

    /// <summary>Decimal degrees, east +ve.</summary>
    public double Longitude { get; }

    public CoursePointType Type { get; }

    /// <summary>Default/fixed zone radius in km (also the lower bound for variable turnpoints).</summary>
    public double DefaultRadiusKm { get; }

    /// <summary>
    /// The observation zone this point was read from, if the course came from a
    /// .cup task. Carries the zone's original tokens so the Planner can re-emit it
    /// verbatim, changing only the barrel radius. Null when the course was built
    /// without source zones.
    /// </summary>
    public ObservationZone? SourceZone { get; }

    /// <summary>
    /// Per-turnpoint lower barrel bound (km), if the task specifies one; null means
    /// use the optimiser's global <c>RMinKm</c>. Read from the waypoint's userdata.
    /// </summary>
    public double? RMinKm { get; }

    /// <summary>
    /// Per-turnpoint upper barrel bound (km), if the task specifies one; null means
    /// use the optimiser's global <c>RMaxKm</c>. Read from the waypoint's userdata.
    /// </summary>
    public double? RMaxKm { get; }

    /// <summary>True for an interior turnpoint whose barrel may be resized.</summary>
    public bool IsVariableCandidate => Type == CoursePointType.Turnpoint;
}

/// <summary>The baseline race course: an ordered list of <see cref="CoursePoint"/>.</summary>
public class Course
{
    private readonly List<CoursePoint> points;

    public Course(IEnumerable<CoursePoint> points, string description = "",
        IReadOnlyList<string>? sourceTaskNames = null, string? optionsLine = null,
        IReadOnlyList<Waypoint>? sourceWaypoints = null)
    {
        this.points = new List<CoursePoint>(points);
        Description = description;
        SourceTaskNames = sourceTaskNames;
        OptionsLine = optionsLine;
        SourceWaypoints = sourceWaypoints;
    }

    public string Description { get; }

    public IReadOnlyList<CoursePoint> Points => points;

    /// <summary>
    /// The original task-line names (including any takeoff/landing entries) from
    /// the source .cup, so the Planner can reproduce the task line verbatim. Null
    /// when the course was not built from a .cup task line.
    /// </summary>
    public IReadOnlyList<string>? SourceTaskNames { get; }

    /// <summary>The source task's <c>Options,…</c> line, preserved for re-emit. Null if absent.</summary>
    public string? OptionsLine { get; }

    /// <summary>
    /// The source .cup's waypoint table, preserved so the Planner can re-emit it
    /// verbatim (codes, elevations, styles) rather than synthesising one. Null when
    /// the course was not built from a .cup file.
    /// </summary>
    public IReadOnlyList<Waypoint>? SourceWaypoints { get; }
}
