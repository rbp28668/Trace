namespace Trace.Data.Entities;

/// <summary>
/// A task set for one class on one day. There are usually an A task and fallback
/// B/C tasks; <b>at most one is <see cref="Active"/> per class per day</b> (zero
/// if the day is scrubbed) — see the note in <c>LogicalClasses.drawio</c>,
/// enforced by a filtered unique index and the task service.
///
/// Named <c>CompetitionTask</c> to avoid clashing with
/// <see cref="System.Threading.Tasks.Task"/> and the IGC <c>Trace.Task</c>
/// declaration type.
/// </summary>
public class CompetitionTask
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Ordering index of this task within the day/class.</summary>
    public int Index { get; set; }

    /// <summary>The one task flown for this class on this day (0 or 1 active).</summary>
    public bool Active { get; set; }

    /// <summary>Task variant, typically "A", "B" or "C".</summary>
    public string TaskType { get; set; } = string.Empty;

    public int DayId { get; set; }

    public Day? Day { get; set; }

    public int CompetitionClassId { get; set; }

    public CompetitionClass? CompetitionClass { get; set; }

    // --- Planning inputs (DHT optimiser) ---

    /// <summary>Planning wind direction, degrees true, FROM. Null until planned.</summary>
    public double? WindDirDeg { get; set; }

    /// <summary>Planning wind speed, km/h. Null until planned.</summary>
    public double? WindSpeedKmh { get; set; }

    /// <summary>Reference handicap used by the optimiser (default: highest in fleet).</summary>
    public double? RefHandicap { get; set; }

    // --- Planning outputs ---

    /// <summary>Reference task distance, km (optimiser output). Null until planned.</summary>
    public double? DRefKm { get; set; }

    /// <summary>Reference task time, seconds (optimiser output). Null until planned.</summary>
    public double? TRefSec { get; set; }

    public List<Turnpoint> Turnpoints { get; set; } = new();

    /// <summary>Per-handicap barrel radii produced by the optimiser.</summary>
    public List<BarrelRadius> BarrelRadii { get; set; } = new();
}
