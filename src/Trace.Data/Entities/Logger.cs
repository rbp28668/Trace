namespace Trace.Data.Entities;

/// <summary>
/// A flight-recorder logger fitted to a glider. A glider may carry more than one
/// (backup loggers). From <c>LogicalClasses.drawio</c> (Logger).
/// </summary>
public class Logger
{
    public int Id { get; set; }

    /// <summary>Logger type/manufacturer, e.g. "LX", "Flarm".</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Logger serial / device id as recorded in the IGC A record.</summary>
    public string LoggerId { get; set; } = string.Empty;

    public int GliderId { get; set; }

    public Glider? Glider { get; set; }
}
