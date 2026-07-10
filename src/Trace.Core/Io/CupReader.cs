using System.Globalization;
using Trace.Model;

namespace Trace.Io;

/// <summary>
/// Reads SeeYou .CUP files: the waypoint table and the optional
/// <c>-----Related Tasks-----</c> section (dht.md §2.2, §5.1).
/// Spec: https://github.com/naviter/seeyou_file_formats/blob/main/CUP_file_format.md
/// </summary>
public class CupReader
{
    private const string TaskSeparator = "-----Related Tasks-----";

    public List<Waypoint> Waypoints { get; } = new();

    public List<CupTask> Tasks { get; } = new();

    public void Parse(string path)
    {
        using var reader = new StreamReader(path);
        Parse(reader);
    }

    public void Parse(TextReader reader)
    {
        Waypoints.Clear();
        Tasks.Clear();

        bool inTasks = false;
        bool headerSeen = false;
        CupTaskBuilder? current = null;

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith(TaskSeparator, StringComparison.Ordinal))
            {
                inTasks = true;
                continue;
            }

            if (!inTasks)
            {
                List<string> fields = SplitCsv(line);
                // Skip the column header row.
                if (!headerSeen && fields.Count > 0 &&
                    fields[0].Equals("name", StringComparison.OrdinalIgnoreCase))
                {
                    headerSeen = true;
                    continue;
                }

                Waypoint? wp = ParseWaypoint(fields);
                if (wp != null)
                {
                    Waypoints.Add(wp);
                }
            }
            else
            {
                current = ParseTaskLine(line, current);
            }
        }

        current?.Commit(Tasks);
    }

    private CupTaskBuilder? ParseTaskLine(string line, CupTaskBuilder? current)
    {
        if (line.StartsWith("ObsZone=", StringComparison.OrdinalIgnoreCase))
        {
            current?.AddZone(ParseObsZone(line));
            return current;
        }

        if (line.StartsWith("Options", StringComparison.OrdinalIgnoreCase))
        {
            // The DHT engine does not interpret task options, but we retain the
            // line verbatim so a task can be re-emitted faithfully.
            current?.SetOptions(line);
            return current;
        }

        // Otherwise this is a new task definition line: a list of quoted names.
        // Commit the previous task (if any) before starting the next.
        current?.Commit(Tasks);
        List<string> names = SplitCsv(line);
        string description = names.Count > 0 ? names[0] : string.Empty;
        var waypointNames = names.Skip(1).ToList();
        return new CupTaskBuilder(description, waypointNames);
    }

    private static ObservationZone ParseObsZone(string line)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var tokens = new List<KeyValuePair<string, string>>();
        foreach (string part in line.Split(','))
        {
            int eq = part.IndexOf('=');
            if (eq > 0)
            {
                string key = part.Substring(0, eq).Trim();
                string value = part.Substring(eq + 1).Trim();
                fields[key] = value;
                tokens.Add(new KeyValuePair<string, string>(key, value));
            }
        }

        return new ObservationZone
        {
            PointIndex = GetInt(fields, "ObsZone", 0),
            Style = (ZoneStyle)GetInt(fields, "Style", (int)ZoneStyle.Symmetrical),
            R1Metres = GetLength(fields, "R1"),
            A1Degrees = GetDouble(fields, "A1", 180.0),
            R2Metres = GetLength(fields, "R2"),
            A2Degrees = GetDouble(fields, "A2", 0.0),
            A12Degrees = GetDouble(fields, "A12", 0.0),
            IsLine = GetInt(fields, "Line", 0) == 1,
            RawTokens = tokens,
        };
    }

    private static Waypoint? ParseWaypoint(List<string> f)
    {
        // name,code,country,lat,lon,elev,style,...
        if (f.Count < 6)
        {
            return null;
        }

        try
        {
            double lat = ParseLatitude(f[3]);
            double lon = ParseLongitude(f[4]);
            double elev = ParseLength(f[5]);
            int style = f.Count > 6 && int.TryParse(f[6], NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int s) ? s : 1;
            string desc = f.Count > 11 ? f[11] : string.Empty;
            string userData = f.Count > 12 ? f[12] : string.Empty;
            return new Waypoint(f[0], f[1], lat, lon, style, elev, desc, userData);
        }
        catch (FormatException)
        {
            return null; // skip malformed rows rather than aborting the file
        }
    }

    /// <summary>Parses <c>DDMM.mmmN</c> latitude into decimal degrees (north +ve).</summary>
    public static double ParseLatitude(string text)
    {
        text = text.Trim();
        char hemi = char.ToUpperInvariant(text[^1]);
        int degrees = int.Parse(text.Substring(0, 2), CultureInfo.InvariantCulture);
        double minutes = double.Parse(text.Substring(2, text.Length - 3), CultureInfo.InvariantCulture);
        double dd = degrees + minutes / 60.0;
        return hemi == 'S' ? -dd : dd;
    }

    /// <summary>Parses <c>DDDMM.mmmW</c> longitude into decimal degrees (east +ve).</summary>
    public static double ParseLongitude(string text)
    {
        text = text.Trim();
        char hemi = char.ToUpperInvariant(text[^1]);
        int degrees = int.Parse(text.Substring(0, 3), CultureInfo.InvariantCulture);
        double minutes = double.Parse(text.Substring(3, text.Length - 4), CultureInfo.InvariantCulture);
        double dd = degrees + minutes / 60.0;
        return hemi == 'W' ? -dd : dd;
    }

    private static double ParseLength(string text)
    {
        text = text.Trim();
        if (text.Length == 0)
        {
            return 0.0;
        }

        if (text.EndsWith("km", StringComparison.OrdinalIgnoreCase))
        {
            return double.Parse(text[..^2], CultureInfo.InvariantCulture) * 1000.0;
        }

        if (text.EndsWith("nm", StringComparison.OrdinalIgnoreCase))
        {
            return double.Parse(text[..^2], CultureInfo.InvariantCulture) * 1852.0;
        }

        if (text.EndsWith("ft", StringComparison.OrdinalIgnoreCase))
        {
            return double.Parse(text[..^2], CultureInfo.InvariantCulture) * 0.3048;
        }

        if (text.EndsWith("m", StringComparison.OrdinalIgnoreCase))
        {
            text = text[..^1];
        }

        return double.Parse(text, CultureInfo.InvariantCulture);
    }

    private static int GetInt(IReadOnlyDictionary<string, string> f, string key, int fallback)
        => f.TryGetValue(key, out string? v) &&
           int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)
            ? n : fallback;

    private static double GetDouble(IReadOnlyDictionary<string, string> f, string key, double fallback)
        => f.TryGetValue(key, out string? v) &&
           double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double n)
            ? n : fallback;

    private static double GetLength(IReadOnlyDictionary<string, string> f, string key)
        => f.TryGetValue(key, out string? v) ? ParseLength(v) : 0.0;

    /// <summary>
    /// Splits a CUP/CSV line, honouring double-quoted fields (which may contain
    /// commas) and <c>""</c> as an escaped quote.
    /// </summary>
    public static List<string> SplitCsv(string line)
    {
        var fields = new List<string>();
        var sb = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                fields.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        fields.Add(sb.ToString());
        return fields;
    }

    private sealed class CupTaskBuilder
    {
        private readonly string description;
        private readonly List<string> names;
        private readonly List<ObservationZone> zones = new();
        private string? optionsLine;

        public CupTaskBuilder(string description, List<string> names)
        {
            this.description = description;
            this.names = names;
        }

        public void AddZone(ObservationZone zone) => zones.Add(zone);

        public void SetOptions(string line) => optionsLine = line;

        public void Commit(List<CupTask>? tasks)
        {
            tasks?.Add(new CupTask(description, names, zones, optionsLine));
        }
    }
}
