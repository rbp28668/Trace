namespace Trace.Data.Entities;

/// <summary>
/// The root aggregate: one gliding competition. Owns its classes and days. Only
/// one competition is <see cref="IsActive"/> at a time, but the schema retains
/// past competitions. See <c>docs/data-app-plan.md</c> §3.
/// </summary>
public class Competition
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Competition site / airfield, e.g. "Aston Down".</summary>
    public string? Site { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    /// <summary>True for the one competition currently being run.</summary>
    public bool IsActive { get; set; }

    public List<CompetitionClass> Classes { get; set; } = new();

    public List<Day> Days { get; set; } = new();
}
