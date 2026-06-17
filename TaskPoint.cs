using System.Globalization;
using System.Text;

namespace Trace;

/// <summary>
/// A single point (turnpoint, start, finish, takeoff or landing) within an IGC
/// task declaration — a waypoint "C Record". Coordinate conventions match
/// <see cref="TracePoint"/>: west is +ve, north is +ve.
/// </summary>
public class TaskPoint
{
    public TaskPoint()
    {
        Name = string.Empty;
    }

    /// <summary>
    /// Parses a waypoint C record line, e.g. <c>C5210900N00006700WTurnpoint 1</c>.
    /// </summary>
    public TaskPoint(string cRecord)
    {
        if (cRecord[0] != 'C')
        {
            throw new FormatException("Not a C record");
        }

        string northingsText = cRecord.Substring(1, 7);
        char ns = cRecord[8];
        int degreesNorth = int.Parse(northingsText.Substring(0, 2), CultureInfo.InvariantCulture); // 00 -> 90
        double minutesNorth = double.Parse(northingsText.Substring(2), CultureInfo.InvariantCulture) / 1000.0;
        Northings = degreesNorth + minutesNorth / 60.0;
        if (char.ToUpperInvariant(ns) == 'S')
        {
            Northings = -Northings;
        }

        string eastingsText = cRecord.Substring(9, 8);
        char ew = cRecord[17];
        int degreesEast = int.Parse(eastingsText.Substring(0, 3), CultureInfo.InvariantCulture); // 000 -> 180
        double minutesEast = double.Parse(eastingsText.Substring(3), CultureInfo.InvariantCulture) / 1000.0;
        Eastings = degreesEast + minutesEast / 60.0;
        if (char.ToUpperInvariant(ew) == 'W')
        {
            Eastings = -Eastings;
        }

        Name = cRecord.Length > 18 ? cRecord.Substring(18) : string.Empty;
    }

    public TaskPoint(double eastings, double northings, string name)
    {
        Eastings = eastings;
        Northings = northings;
        Name = name;
    }

    /// <summary>Decimal degrees, west is +ve.</summary>
    public double Eastings { get; }

    /// <summary>Decimal degrees, north is +ve.</summary>
    public double Northings { get; }

    public string Name { get; }

    public void Write(TextWriter os)
    {
        var sb = new StringBuilder();
        sb.Append('C');

        // Northings
        char ns = Northings > 0 ? 'N' : 'S';
        double lat = Math.Abs(Northings);
        int degrees = (int)Math.Floor(lat);
        lat -= degrees;
        lat *= 60; // now in minutes
        int minutes = (int)Math.Floor(lat * 1000);

        sb.Append(degrees.ToString("D2", CultureInfo.InvariantCulture));
        sb.Append(minutes.ToString("D5", CultureInfo.InvariantCulture));
        sb.Append(ns);

        // Eastings
        char ew = Eastings > 0 ? 'E' : 'W';
        double lng = Math.Abs(Eastings);
        degrees = (int)Math.Floor(lng);
        lng -= degrees;
        lng *= 60; // to minutes
        minutes = (int)Math.Floor(lng * 1000);

        sb.Append(degrees.ToString("D3", CultureInfo.InvariantCulture));
        sb.Append(minutes.ToString("D5", CultureInfo.InvariantCulture));
        sb.Append(ew);

        sb.Append(Name);

        os.WriteLine(sb.ToString());
    }
}
