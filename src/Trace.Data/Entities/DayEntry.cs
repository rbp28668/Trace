namespace Trace.Data.Entities;

/// <summary>
/// Which pilot flew which glider in which class on a given day, against the
/// active task. From <c>LogicalClasses.drawio</c> (DayEntry). Carries the
/// resulting <see cref="Flight"/> (0..1) once a log is scored.
/// </summary>
public class DayEntry
{
    public int Id { get; set; }

    public int DayId { get; set; }

    public Day? Day { get; set; }

    public int CompetitionClassId { get; set; }

    public CompetitionClass? CompetitionClass { get; set; }

    public int PilotId { get; set; }

    public Pilot? Pilot { get; set; }

    public int GliderId { get; set; }

    public Glider? Glider { get; set; }

    /// <summary>The task flown (normally the active task for the class that day).</summary>
    public int? CompetitionTaskId { get; set; }

    public CompetitionTask? CompetitionTask { get; set; }

    public Flight? Flight { get; set; }
}
