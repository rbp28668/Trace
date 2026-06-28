namespace Trace.Airspace;

/// <summary>
/// A single airspace volume read from an OpenAir file: a class, a name, vertical
/// limits, and a boundary polygon (circles and arcs are tessellated to vertices
/// on load). Coordinates are decimal degrees, latitude north +ve, longitude
/// east +ve.
/// </summary>
public class AirspaceVolume
{
    public AirspaceVolume(string airspaceClass, string name, IReadOnlyList<GeoPoint> boundary,
        double lowerLimitFt, double upperLimitFt)
    {
        Class = airspaceClass;
        Name = name;
        Boundary = boundary;
        LowerLimitFt = lowerLimitFt;
        UpperLimitFt = upperLimitFt;

        double minLat = double.MaxValue, minLon = double.MaxValue;
        double maxLat = double.MinValue, maxLon = double.MinValue;
        foreach (GeoPoint p in boundary)
        {
            if (p.Latitude < minLat) minLat = p.Latitude;
            if (p.Latitude > maxLat) maxLat = p.Latitude;
            if (p.Longitude < minLon) minLon = p.Longitude;
            if (p.Longitude > maxLon) maxLon = p.Longitude;
        }

        MinLatitude = minLat;
        MaxLatitude = maxLat;
        MinLongitude = minLon;
        MaxLongitude = maxLon;
    }

    /// <summary>OpenAir class token, e.g. "CTR", "TMA", "A", "D".</summary>
    public string Class { get; }

    public string Name { get; }

    /// <summary>Boundary polygon vertices (closed implicitly).</summary>
    public IReadOnlyList<GeoPoint> Boundary { get; }

    /// <summary>Lower vertical limit in feet (SFC = 0).</summary>
    public double LowerLimitFt { get; }

    /// <summary>Upper vertical limit in feet (FL converted at 100 ft per level).</summary>
    public double UpperLimitFt { get; }

    public double MinLatitude { get; }
    public double MaxLatitude { get; }
    public double MinLongitude { get; }
    public double MaxLongitude { get; }
}

/// <summary>A geographic point in decimal degrees (latitude north +ve, longitude east +ve).</summary>
public readonly record struct GeoPoint(double Latitude, double Longitude);
