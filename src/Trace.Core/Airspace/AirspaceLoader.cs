namespace Trace.Airspace;

/// <summary>
/// Loads one or more OpenAir files into a single <see cref="AirspaceChecker"/>
/// (airspace.md: "It should be possible to read multiple airspace files").
/// </summary>
public static class AirspaceLoader
{
    public static AirspaceChecker Load(IEnumerable<string> paths)
    {
        var reader = new OpenAirReader();
        foreach (string path in paths)
        {
            reader.Parse(path);
        }

        return new AirspaceChecker(reader.Airspaces);
    }

    /// <summary>Default controlled-airspace classes guarded for DHT barrels (dht.md §5.2).</summary>
    public static IReadOnlySet<string> ControlledClasses { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CTR", "TMA", "CTA", "A", "C", "D" };
}
