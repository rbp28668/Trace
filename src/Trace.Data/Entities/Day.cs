namespace Trace.Data.Entities;

/// <summary>
/// A competition day. From <c>LogicalClasses.drawio</c> (Day). Tasks are defined
/// per class within a day; day entries record who flew what.
/// </summary>
public class Day
{
    public int Id { get; set; }

    /// <summary>Sequential day number within the competition (1-based).</summary>
    public int DayNo { get; set; }

    public DateOnly Date { get; set; }

    public int CompetitionId { get; set; }

    public Competition? Competition { get; set; }

    public List<CompetitionTask> Tasks { get; set; } = new();

    public List<DayEntry> Entries { get; set; } = new();
}
