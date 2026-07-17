namespace Trace.Data.Entities;

/// <summary>
/// One point of a task course: start, turnpoint, checkpoint or finish, with its
/// observation-zone geometry. From <c>LogicalClasses.drawio</c> (Turnpoint);
/// maps to/from <see cref="Trace.Model.CoursePoint"/> and
/// <see cref="Trace.Model.ObservationZone"/>.
/// </summary>
public class Turnpoint
{
    public int Id { get; set; }

    /// <summary>Ordering index within the task (0-based, start first).</summary>
    public int Index { get; set; }

    /// <summary>Waypoint name, e.g. "Lasham".</summary>
    public string Waypoint { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    /// <summary>A mandatory fly-through checkpoint (not a scoring turnpoint).</summary>
    public bool IsCheckpoint { get; set; }

    /// <summary>The zone is a line (start/finish line) rather than a barrel/sector.</summary>
    public bool IsLine { get; set; }

    /// <summary>CUP observation-zone style code.</summary>
    public int Style { get; set; }

    /// <summary>CUP zone direction type (symmetric / fixed / to-next / to-prev).</summary>
    public int DirectionType { get; set; }

    /// <summary>Primary zone radius, km (the barrel radius for a turnpoint).</summary>
    public double Radius1 { get; set; }

    /// <summary>Primary sector half-angle, degrees.</summary>
    public double Angle1 { get; set; }

    /// <summary>Secondary zone radius, km (0 if unused).</summary>
    public double Radius2 { get; set; }

    /// <summary>Secondary sector angle, degrees.</summary>
    public double Angle2 { get; set; }

    public int CompetitionTaskId { get; set; }

    public CompetitionTask? CompetitionTask { get; set; }
}
