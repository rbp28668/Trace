using System.Globalization;
using System.Text;
using Trace.Model;

namespace Trace.Io;

/// <summary>
/// Writes standard SeeYou .CUP files (dht.md §5.1): a waypoint table followed by
/// a <c>-----Related Tasks-----</c> section. The DHT engine emits one task file
/// per handicap, each with fixed observation-zone radii — no proprietary
/// per-handicap fields.
/// Spec: https://github.com/naviter/seeyou_file_formats/blob/main/CUP_file_format.md
/// </summary>
public class CupWriter
{
    private const string Header = "name,code,country,lat,lon,elev,style,rwdir,rwlen,rwwidth,freq,desc,userdata,pics";
    private const string TaskSeparator = "-----Related Tasks-----";

    public void Write(TextWriter os, IReadOnlyList<Waypoint> waypoints, CupTask? task = null)
    {
        os.WriteLine(Header);
        foreach (Waypoint wp in waypoints)
        {
            os.WriteLine(FormatWaypoint(wp));
        }

        if (task != null)
        {
            os.WriteLine(TaskSeparator);
            os.WriteLine(FormatTaskLine(task));
            foreach (ObservationZone zone in task.Zones)
            {
                os.WriteLine(FormatObsZone(zone));
            }
        }
    }

    private static string FormatWaypoint(Waypoint wp)
    {
        var sb = new StringBuilder();
        sb.Append(Quote(wp.Name)).Append(',');
        sb.Append(Quote(wp.Code)).Append(',');
        sb.Append(',');                                  // country
        sb.Append(FormatLatitude(wp.Latitude)).Append(',');
        sb.Append(FormatLongitude(wp.Longitude)).Append(',');
        sb.Append(((int)wp.ElevationM).ToString(CultureInfo.InvariantCulture)).Append("m,");
        sb.Append(wp.Style.ToString(CultureInfo.InvariantCulture)).Append(',');
        sb.Append(",,,,");                               // rwdir,rwlen,rwwidth,freq
        sb.Append(Quote(wp.Description)).Append(',');
        sb.Append(',');                                  // userdata
        // pics (trailing field left empty)
        return sb.ToString();
    }

    private static string FormatTaskLine(CupTask task)
    {
        var sb = new StringBuilder();
        sb.Append(Quote(task.Description));
        foreach (string name in task.WaypointNames)
        {
            sb.Append(',').Append(Quote(name));
        }

        return sb.ToString();
    }

    private static string FormatObsZone(ObservationZone z)
    {
        var sb = new StringBuilder();
        sb.Append("ObsZone=").Append(z.PointIndex.ToString(CultureInfo.InvariantCulture));
        sb.Append(",Style=").Append(((int)z.Style).ToString(CultureInfo.InvariantCulture));
        sb.Append(",R1=").Append(FormatMetres(z.R1Metres));
        sb.Append(",A1=").Append(FormatAngle(z.A1Degrees));

        if (z.R2Metres > 0.0)
        {
            sb.Append(",R2=").Append(FormatMetres(z.R2Metres));
            sb.Append(",A2=").Append(FormatAngle(z.A2Degrees));
        }

        if (z.Style == ZoneStyle.Fixed)
        {
            sb.Append(",A12=").Append(FormatAngle(z.A12Degrees));
        }

        if (z.IsLine)
        {
            sb.Append(",Line=1");
        }

        return sb.ToString();
    }

    /// <summary>Formats latitude as <c>DDMM.mmmN</c>.</summary>
    public static string FormatLatitude(double lat)
    {
        char hemi = lat >= 0 ? 'N' : 'S';
        double a = Math.Abs(lat);
        int deg = (int)a;
        double min = (a - deg) * 60.0;
        return deg.ToString("D2", CultureInfo.InvariantCulture) +
               min.ToString("00.000", CultureInfo.InvariantCulture) + hemi;
    }

    /// <summary>Formats longitude as <c>DDDMM.mmmW</c>.</summary>
    public static string FormatLongitude(double lon)
    {
        char hemi = lon >= 0 ? 'E' : 'W';
        double a = Math.Abs(lon);
        int deg = (int)a;
        double min = (a - deg) * 60.0;
        return deg.ToString("D3", CultureInfo.InvariantCulture) +
               min.ToString("00.000", CultureInfo.InvariantCulture) + hemi;
    }

    private static string FormatMetres(double metres)
        => ((int)Math.Round(metres)).ToString(CultureInfo.InvariantCulture) + "m";

    private static string FormatAngle(double degrees)
        => degrees.ToString("0.#", CultureInfo.InvariantCulture);

    private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
}
