namespace Trace.Data.Entities;

/// <summary>
/// Links a glider to the pilot(s) who fly it in a class for the whole
/// competition ("which glider is entered, and who may fly it"). The pilot roster
/// is ordered via <see cref="EntryPilot"/> — the first pilot is the primary (P1).
/// From <c>LogicalClasses.drawio</c> (CompetitionEntry).
/// </summary>
public class CompetitionEntry
{
    public int Id { get; set; }

    public int CompetitionClassId { get; set; }

    public CompetitionClass? CompetitionClass { get; set; }

    public int GliderId { get; set; }

    public Glider? Glider { get; set; }

    /// <summary>Ordered roster of pilots entered on this glider (first = primary).</summary>
    public List<EntryPilot> Pilots { get; set; } = new();
}
