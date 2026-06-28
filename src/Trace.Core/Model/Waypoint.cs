namespace Trace.Model;

/// <summary>
/// A SeeYou .CUP waypoint. Coordinates are decimal degrees, latitude north +ve,
/// longitude east +ve (west −ve).
/// </summary>
public class Waypoint
{
    public Waypoint(string name, string code, double latitude, double longitude,
        int style = 1, double elevationM = 0.0, string description = "")
    {
        Name = name;
        Code = code;
        Latitude = latitude;
        Longitude = longitude;
        Style = style;
        ElevationM = elevationM;
        Description = description;
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
}
