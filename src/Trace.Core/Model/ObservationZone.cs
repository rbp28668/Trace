namespace Trace.Model;

/// <summary>
/// Direction the observation-zone bisector points (CUP <c>Style</c> codes for an
/// <c>ObsZone</c> line).
/// </summary>
public enum ZoneStyle
{
    Fixed = 0,
    Symmetrical = 1,
    ToNext = 2,
    ToPrevious = 3,
    ToStart = 4,
}

/// <summary>
/// A turnpoint observation zone in the SeeYou .CUP sense (dht.md §5.1). For DHT
/// tasks the engine emits a simple cylinder: <c>Style=Symmetrical, R1=radius,
/// A1=180</c>. Lines (start/finish) set <see cref="IsLine"/> with R1 the half
/// width.
/// </summary>
public class ObservationZone
{
    /// <summary>Index of the task point this zone belongs to (0 = start).</summary>
    public int PointIndex { get; init; }

    public ZoneStyle Style { get; init; } = ZoneStyle.Symmetrical;

    /// <summary>Primary radius R1 in metres.</summary>
    public double R1Metres { get; init; }

    /// <summary>Primary angle A1 in degrees (180 = full cylinder).</summary>
    public double A1Degrees { get; init; } = 180.0;

    /// <summary>Secondary radius R2 in metres (0 if unused).</summary>
    public double R2Metres { get; init; }

    /// <summary>Secondary angle A2 in degrees (0 if unused).</summary>
    public double A2Degrees { get; init; }

    /// <summary>Bisector angle A12 in degrees (used with <see cref="ZoneStyle.Fixed"/>).</summary>
    public double A12Degrees { get; init; }

    /// <summary>True for a line zone (start/finish line); R1 is the half width.</summary>
    public bool IsLine { get; init; }

    /// <summary>
    /// The zone's original <c>ObsZone=…</c> tokens in file order (including the
    /// leading <c>ObsZone</c> key), captured when read from a file. Lets the writer
    /// re-emit a zone verbatim — preserving fields the engine does not model
    /// (SpeedStyle, MaxAlt, …) — while substituting only R1. Null for zones the
    /// engine synthesises.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string>>? RawTokens { get; init; }

    /// <summary>
    /// Returns a copy of this zone with its R1 set to <paramref name="metres"/>,
    /// updating the matching <see cref="RawTokens"/> entry in place so a verbatim
    /// re-emit carries the new radius.
    /// </summary>
    public ObservationZone WithR1(double metres)
    {
        List<KeyValuePair<string, string>>? tokens = RawTokens?.ToList();
        if (tokens != null)
        {
            string value = ((int)Math.Round(metres)).ToString(System.Globalization.CultureInfo.InvariantCulture) + "m";
            bool replaced = false;
            for (int i = 0; i < tokens.Count; i++)
            {
                if (tokens[i].Key.Equals("R1", StringComparison.OrdinalIgnoreCase))
                {
                    tokens[i] = new KeyValuePair<string, string>(tokens[i].Key, value);
                    replaced = true;
                    break;
                }
            }

            if (!replaced)
            {
                tokens.Add(new KeyValuePair<string, string>("R1", value));
            }
        }

        return new ObservationZone
        {
            PointIndex = PointIndex,
            Style = Style,
            R1Metres = metres,
            A1Degrees = A1Degrees,
            R2Metres = R2Metres,
            A2Degrees = A2Degrees,
            A12Degrees = A12Degrees,
            IsLine = IsLine,
            RawTokens = tokens,
        };
    }

    /// <summary>Creates a symmetric cylinder zone of the given radius (km).</summary>
    public static ObservationZone Cylinder(int pointIndex, double radiusKm) => new()
    {
        PointIndex = pointIndex,
        Style = ZoneStyle.Symmetrical,
        R1Metres = radiusKm * 1000.0,
        A1Degrees = 180.0,
    };

    /// <summary>Creates a start/finish line zone of the given half width (km).</summary>
    public static ObservationZone LineZone(int pointIndex, double halfWidthKm) => new()
    {
        PointIndex = pointIndex,
        Style = ZoneStyle.ToNext,
        R1Metres = halfWidthKm * 1000.0,
        A1Degrees = 90.0,
        IsLine = true,
    };
}
