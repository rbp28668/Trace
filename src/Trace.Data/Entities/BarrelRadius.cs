namespace Trace.Data.Entities;

/// <summary>
/// One optimiser output cell: the barrel radius (km) applied at a given
/// turnpoint for gliders of a given handicap. Together these rows let the app
/// regenerate a per-handicap <c>.cup</c> task file. See <c>data-app-plan.md</c> §3.
/// </summary>
public class BarrelRadius
{
    public int Id { get; set; }

    /// <summary>Handicap band this radius applies to (BGA scheme, baseline 100).</summary>
    public double Handicap { get; set; }

    /// <summary>Zero-based turnpoint index within the task.</summary>
    public int TurnpointIndex { get; set; }

    public double RadiusKm { get; set; }

    public int CompetitionTaskId { get; set; }

    public CompetitionTask? CompetitionTask { get; set; }
}
