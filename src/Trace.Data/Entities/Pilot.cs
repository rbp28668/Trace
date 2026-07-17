namespace Trace.Data.Entities;

/// <summary>
/// A competing pilot. From <c>LogicalClasses.drawio</c> (Pilot). Shared across
/// classes within a competition via <see cref="CompetitionEntry"/>.
/// </summary>
public class Pilot
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>BGA / competition account number.</summary>
    public int? AccountNo { get; set; }

    public List<CompetitionEntry> Entries { get; set; } = new();
}
