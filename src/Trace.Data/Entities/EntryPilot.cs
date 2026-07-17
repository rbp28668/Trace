namespace Trace.Data.Entities;

/// <summary>
/// Join row placing a pilot on a competition entry's roster, in a defined order.
/// <see cref="Order"/> 0 is the primary pilot (P1); higher values are further
/// crew. A pilot may appear on many entries; an entry may list many pilots.
/// </summary>
public class EntryPilot
{
    public int Id { get; set; }

    public int CompetitionEntryId { get; set; }

    public CompetitionEntry? CompetitionEntry { get; set; }

    public int PilotId { get; set; }

    public Pilot? Pilot { get; set; }

    /// <summary>Position in the roster; 0 = primary (P1).</summary>
    public int Order { get; set; }
}
