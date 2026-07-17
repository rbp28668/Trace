namespace Trace.Data.Entities;

/// <summary>
/// The scored result of a day entry's flight. From <c>LogicalClasses.drawio</c>
/// (Flight). Populated by the scoring engine from the uploaded IGC log.
/// </summary>
public class Flight
{
    public int Id { get; set; }

    public int DayEntryId { get; set; }

    public DayEntry? DayEntry { get; set; }

    /// <summary>Start time (task clock).</summary>
    public TimeOnly? Start { get; set; }

    /// <summary>Finish time (task clock); null for a land-out.</summary>
    public TimeOnly? Finish { get; set; }

    /// <summary>Task time (finish − start), for a finisher.</summary>
    public TimeSpan? Time { get; set; }

    /// <summary>Scoring speed, km/h (finisher).</summary>
    public double? Speed { get; set; }

    /// <summary>Scoring distance, km (land-out; handicapped final distance).</summary>
    public double? Distance { get; set; }

    /// <summary>True if the trace stayed clear of controlled airspace.</summary>
    public bool AirspaceValid { get; set; }

    /// <summary>True once an airspace check has been run.</summary>
    public bool AirspaceChecked { get; set; }

    public IgcFile? IgcFile { get; set; }
}
