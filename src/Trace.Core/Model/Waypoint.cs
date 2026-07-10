namespace Trace.Model;

/// <summary>
/// A SeeYou .CUP waypoint. Coordinates are decimal degrees, latitude north +ve,
/// longitude east +ve (west −ve).
/// </summary>
public class Waypoint
{
    public Waypoint(string name, string code, double latitude, double longitude,
        int style = 1, double elevationM = 0.0, string description = "", string userData = "")
    {
        Name = name;
        Code = code;
        Latitude = latitude;
        Longitude = longitude;
        Style = style;
        ElevationM = elevationM;
        Description = description;
        UserData = userData;
    }

    /// <summary>Full waypoint name (column <c>name</c>).</summary>
    public string Name { get; }

    /// <summary>Short code (column <c>code</c>).</summary>
    public string Code { get; }

    /// <summary>Decimal degrees, north +ve.</summary>
    public double Latitude { get; }

    /// <summary>Decimal degrees, east +ve.</summary>
    public double Longitude { get; }

    /// <summary>CUP waypoint style code (1 = waypoint, 4 = gliding airfield, …).</summary>
    public int Style { get; }

    /// <summary>Elevation in metres.</summary>
    public double ElevationM { get; }

    public string Description { get; }

    /// <summary>
    /// Free-text <c>userdata</c> column (CUP field 13). The DHT tools store a small
    /// JSON blob here to carry per-turnpoint barrel bounds, e.g.
    /// <c>{"rmin":0.5,"rmax":10}</c>; other CUP readers ignore it.
    /// </summary>
    public string UserData { get; }
}
