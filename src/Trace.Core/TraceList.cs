namespace Trace;

/// <summary>
/// An ordered collection of <see cref="TracePoint"/> — the C# equivalent of
/// the original <c>std::vector&lt;TracePoint&gt;</c>.
/// </summary>
public class TraceList : List<TracePoint>
{
    public TraceList()
    {
    }

    public TraceList(IEnumerable<TracePoint> points)
        : base(points)
    {
    }
}
