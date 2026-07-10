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
/// A turnpoint observation zone in the SeeYou .CUP sense. Following the SeeYou
/// convention, <c>R1/A1</c> describe the (larger) observation sector — a fix
/// achieves the point if it falls in this sector, oriented by <see cref="Style"/>
/// — and <c>R2/A2</c> describe the (smaller) inner barrel, a full circle used only
/// for the DHT distance calculation. The angle is a half-angle either side of the
/// zone direction, so <c>A1=45</c> is a 90° sector and <c>A1=180</c> a full circle.
/// A simple task written without R2 uses R1 as both sector and barrel (a plain
/// cylinder). Lines (start/finish) set <see cref="IsLine"/> with R1 the half width.
/// See docs/CUP_file_format.md and https://docs.rs/seeyou-cup.
/// </summary>
public class ObservationZone
{
    /// <summary>Index of the task point this zone belongs to (0 = start).</summary>
    public int PointIndex { get; init; }

    public ZoneStyle Style { get; init; } = ZoneStyle.Symmetrical;

    /// <summary>Sector radius R1 in metres (the observation sector's outer radius).</summary>
    public double R1Metres { get; init; }

    /// <summary>Sector half-angle A1 in degrees, either side of the direction (180 = full circle).</summary>
    public double A1Degrees { get; init; } = 180.0;

    /// <summary>Barrel radius R2 in metres, a full inner circle (0 if unused).</summary>
    public double R2Metres { get; init; }

    /// <summary>Barrel half-angle A2 in degrees (barrels are full circles: 180).</summary>
    public double A2Degrees { get; init; }

    /// <summary>
    /// The DHT barrel radius in metres: the inner R2 circle when present, otherwise
    /// R1 (a plain cylinder). This is the radius the optimiser sizes and the scorer
    /// uses for distance; achieving the point uses the wider sector too.
    /// </summary>
    public double BarrelRadiusMetres => R2Metres > 0.0 ? R2Metres : R1Metres;

    /// <summary>The observation sector's outer radius in metres (R1).</summary>
    public double SectorRadiusMetres => R1Metres;

    /// <summary>The observation sector's half-angle in degrees (A1).</summary>
    public double SectorHalfAngleDegrees => A1Degrees;

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
    /// Returns a copy of this zone with the DHT barrel resized to
    /// <paramref name="metres"/>. When the zone has a distinct sector (an R2 token
    /// or a non-full A1 sector), the barrel is R2 and only R2 is rewritten, leaving
    /// the observation sector R1/A1 untouched. For a plain cylinder (no sector) the
    /// barrel is R1. <see cref="RawTokens"/> are updated so a verbatim re-emit
    /// carries the new radius.
    /// </summary>
    public ObservationZone WithBarrel(double metres)
    {
        bool hasSector = R2Metres > 0.0 || A1Degrees < 179.999;
        string barrelKey = hasSector ? "R2" : "R1";

        List<KeyValuePair<string, string>>? tokens = RawTokens?.ToList();
        if (tokens != null)
        {
            string value = ((int)Math.Round(metres)).ToString(System.Globalization.CultureInfo.InvariantCulture) + "m";
            bool replaced = false;
            for (int i = 0; i < tokens.Count; i++)
            {
                if (tokens[i].Key.Equals(barrelKey, StringComparison.OrdinalIgnoreCase))
                {
                    tokens[i] = new KeyValuePair<string, string>(tokens[i].Key, value);
                    replaced = true;
                    break;
                }
            }

            if (!replaced)
            {
                tokens.Add(new KeyValuePair<string, string>(barrelKey, value));
            }
        }

        return new ObservationZone
        {
            PointIndex = PointIndex,
            Style = Style,
            R1Metres = hasSector ? R1Metres : metres,
            A1Degrees = A1Degrees,
            R2Metres = hasSector ? metres : R2Metres,
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
