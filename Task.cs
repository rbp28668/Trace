using System.Globalization;
using System.Text;

namespace Trace;

/// <summary>
/// An IGC task (flight) declaration, made up of a declaration "C Record"
/// header followed by one waypoint C record per <see cref="TaskPoint"/>.
///
/// The IGC declaration header has the form:
/// <code>C DDMMYYHHMMSS DDMMYY NNNN NN [description]</code>
/// i.e. UTC date/time of declaration, the (optional) flight date, the task
/// number that day, the number of turnpoints, and a free-text description.
/// The turnpoint count excludes the takeoff and landing points, matching the
/// IGC specification (the waypoint list is takeoff, start, turnpoints...,
/// finish, landing).
/// </summary>
public class Task
{
    private readonly List<TaskPoint> points = new();

    public Task()
    {
        Description = string.Empty;
    }

    /// <summary>UTC date/time the task was declared.</summary>
    public DateTime DeclarationTime { get; set; }

    /// <summary>
    /// Date of the flight the task applies to, or <c>null</c> if not given
    /// (encoded as <c>000000</c> in the file).
    /// </summary>
    public DateTime? FlightDate { get; set; }

    /// <summary>Task number on the flight date.</summary>
    public int TaskNumber { get; set; }

    /// <summary>Free-text task description.</summary>
    public string Description { get; set; }

    /// <summary>
    /// The waypoints making up the task. By IGC convention this is
    /// takeoff, start, turnpoint(s), finish, landing.
    /// </summary>
    public List<TaskPoint> Points => points;

    /// <summary>
    /// Parses the declaration header line (the first C record), e.g.
    /// <c>C300515120000300515000102Task</c>.
    /// </summary>
    public void ParseDeclaration(string cRecord)
    {
        if (cRecord[0] != 'C')
        {
            throw new FormatException("Not a C record");
        }

        // C DDMMYY HHMMSS DDMMYY NNNN NN <description>
        string declDate = cRecord.Substring(1, 6);
        string declTime = cRecord.Substring(7, 6);
        DeclarationTime = ParseDateTime(declDate, declTime);

        string flightDate = cRecord.Substring(13, 6);
        FlightDate = flightDate == "000000" ? null : ParseDate(flightDate);

        TaskNumber = int.Parse(cRecord.Substring(19, 4), CultureInfo.InvariantCulture);

        // Number of turnpoints (excludes takeoff/landing) is at 23..24; it is
        // implied by the waypoint records that follow, so it is not stored.

        Description = cRecord.Length > 25 ? cRecord.Substring(25) : string.Empty;
    }

    /// <summary>Adds a waypoint parsed from a (non-header) C record.</summary>
    public void AddPoint(string cRecord) => points.Add(new TaskPoint(cRecord));

    public void Write(TextWriter os)
    {
        var sb = new StringBuilder();
        sb.Append('C');

        DateTime decl = DeclarationTime.ToUniversalTime();
        sb.Append(FormatDate(decl));
        sb.Append(decl.Hour.ToString("D2", CultureInfo.InvariantCulture));
        sb.Append(decl.Minute.ToString("D2", CultureInfo.InvariantCulture));
        sb.Append(decl.Second.ToString("D2", CultureInfo.InvariantCulture));

        sb.Append(FlightDate is { } fd ? FormatDate(fd.ToUniversalTime()) : "000000");

        sb.Append(TaskNumber.ToString("D4", CultureInfo.InvariantCulture));

        // Turnpoint count excludes the takeoff and landing waypoints.
        int turnpoints = Math.Max(0, points.Count - 2);
        sb.Append(turnpoints.ToString("D2", CultureInfo.InvariantCulture));

        sb.Append(Description);

        os.WriteLine(sb.ToString());

        foreach (TaskPoint point in points)
        {
            point.Write(os);
        }
    }

    private static string FormatDate(DateTime d) =>
        d.Day.ToString("D2", CultureInfo.InvariantCulture) +
        d.Month.ToString("D2", CultureInfo.InvariantCulture) +
        (d.Year % 100).ToString("D2", CultureInfo.InvariantCulture);

    private static DateTime ParseDate(string ddmmyy)
    {
        int day = int.Parse(ddmmyy.Substring(0, 2), CultureInfo.InvariantCulture);
        int month = int.Parse(ddmmyy.Substring(2, 2), CultureInfo.InvariantCulture);
        int year = int.Parse(ddmmyy.Substring(4, 2), CultureInfo.InvariantCulture);
        // Y2K style bodge, matching IGCFile.
        year += year < 70 ? 2000 : 1900;
        return new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
    }

    private static DateTime ParseDateTime(string ddmmyy, string hhmmss)
    {
        DateTime date = ParseDate(ddmmyy);
        int hh = int.Parse(hhmmss.Substring(0, 2), CultureInfo.InvariantCulture);
        int mm = int.Parse(hhmmss.Substring(2, 2), CultureInfo.InvariantCulture);
        int ss = int.Parse(hhmmss.Substring(4, 2), CultureInfo.InvariantCulture);
        return new DateTime(date.Year, date.Month, date.Day, hh, mm, ss, DateTimeKind.Utc);
    }
}
