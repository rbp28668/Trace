namespace Trace.Data.Entities;

/// <summary>
/// Links a pilot to the glider they fly in a class for the whole competition
/// ("which pilot is flying which glider(s) in which class"). From
/// <c>LogicalClasses.drawio</c> (CompetitionEntry).
/// </summary>
public class CompetitionEntry
{
    public int Id { get; set; }

    public int CompetitionClassId { get; set; }

    public CompetitionClass? CompetitionClass { get; set; }

    public int PilotId { get; set; }

    public Pilot? Pilot { get; set; }

    public int GliderId { get; set; }

    public Glider? Glider { get; set; }

    /// <summary>Optional second pilot / crew (P2), if the entry is two-seat.</summary>
    public int? P2PilotId { get; set; }

    public Pilot? P2Pilot { get; set; }
}
