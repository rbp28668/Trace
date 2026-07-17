namespace Trace.Data.Entities;

/// <summary>
/// A competition class (e.g. "Racing", "Club"). Scopes its own fleet of gliders
/// and pilots, its competition entries, and its per-day tasks. From
/// <c>LogicalClasses.drawio</c> (CompetitionClass).
/// </summary>
public class CompetitionClass
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Fleet anchor cruise airspeed at H=100, km/h (the DHT engine's
    /// <c>VRefCru</c>), specific to this class's fleet. Maps to
    /// <see cref="Trace.Model.Fleet.VRefCruKmh"/>.
    /// </summary>
    public double VRefCruKmh { get; set; } = 130;

    public int CompetitionId { get; set; }

    public Competition? Competition { get; set; }

    public List<Glider> Gliders { get; set; } = new();

    public List<CompetitionEntry> Entries { get; set; } = new();

    public List<CompetitionTask> Tasks { get; set; } = new();
}
