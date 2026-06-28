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
        CoursePointType type, double defaultRadiusKm)
    {
        Name = name;
        Latitude = latitude;
        Longitude = longitude;
        Type = type;
        DefaultRadiusKm = defaultRadiusKm;
    }

    public string Name { get; }

    /// <summary>Decimal degrees, north +ve.</summary>
    public double Latitude { get; }

    /// <summary>Decimal degrees, east +ve.</summary>
    public double Longitude { get; }

    public CoursePointType Type { get; }

    /// <summary>Default/fixed zone radius in km (also the lower bound for variable turnpoints).</summary>
    public double DefaultRadiusKm { get; }

    /// <summary>True for an interior turnpoint whose barrel may be resized.</summary>
    public bool IsVariableCandidate => Type == CoursePointType.Turnpoint;
}

/// <summary>The baseline race course: an ordered list of <see cref="CoursePoint"/>.</summary>
public class Course
{
    private readonly List<CoursePoint> points;

    public Course(IEnumerable<CoursePoint> points, string description = "")
    {
        this.points = new List<CoursePoint>(points);
        Description = description;
    }

    public string Description { get; }

    public IReadOnlyList<CoursePoint> Points => points;
}
