namespace Trace.Data.Entities;

/// <summary>
/// A competing glider, scoped to a class. From <c>LogicalClasses.drawio</c>
/// (Glider); maps to/from <see cref="Trace.Model.Glider"/> (which carries only
/// <c>Type</c>, <c>Registration</c>, <c>CompNumber</c>, <c>Handicap</c>).
/// </summary>
public class Glider
{
    public int Id { get; set; }

    /// <summary>Competition number, e.g. "NX".</summary>
    public string CompNo { get; set; } = string.Empty;

    /// <summary>Registration, e.g. "G-DDHT".</summary>
    public string? Registration { get; set; }

    /// <summary>Glider type, e.g. "ASW 19".</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Performance index H relative to baseline 100 (BGA scheme).</summary>
    public double Handicap { get; set; }

    /// <summary>ICAO 24-bit address of the primary logger, if known.</summary>
    public int? Icao { get; set; }

    public int CompetitionClassId { get; set; }

    public CompetitionClass? CompetitionClass { get; set; }

    public List<Logger> Loggers { get; set; } = new();
}
