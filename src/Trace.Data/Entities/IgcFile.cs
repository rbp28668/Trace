namespace Trace.Data.Entities;

/// <summary>
/// A stored IGC log uploaded for a flight. From <c>LogicalClasses.drawio</c>
/// (IGCFile). The raw log is kept on disk under the configured data root; this
/// row holds the metadata and relative path (see <c>data-app-plan.md</c> §9).
/// </summary>
public class IgcFile
{
    public int Id { get; set; }

    public int FlightId { get; set; }

    public Flight? Flight { get; set; }

    public string FileName { get; set; } = string.Empty;

    /// <summary>Relative path under the configured IGC data root.</summary>
    public string StoredPath { get; set; } = string.Empty;

    public DateTime UploadedUtc { get; set; }
}
